// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Mcr;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrDocsCommand : ManifestCommand<PublishMcrDocsOptions, PublishMcrDocsOptionsBuilder>
    {
        private const string McrTagsPlaceholder = "Tags go here.";
        private readonly IGitService _gitService;
        private readonly IGitHubClientFactory _gitHubClientFactory;
        private readonly ILogger<PublishMcrDocsCommand> _logger;

        public PublishMcrDocsCommand(IManifestJsonService manifestJsonService, IGitService gitService, IGitHubClientFactory gitHubClientFactory,
            ILogger<PublishMcrDocsCommand> logger) : base(manifestJsonService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override string Description => "Publishes the readmes to MCR";

        public override async Task ExecuteAsync()
        {
            _logger.LogInformation("PUBLISHING MCR DOCS");

            ValidateReadmeFilenames(Manifest);

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            string productRepo = GetProductRepo();

            IEnumerable<GitObject> gitObjects =
                GetUpdatedReadmes(productRepo)
                .Concat(GetUpdatedTagsMetadata(productRepo));

            foreach (GitObject gitObject in gitObjects)
            {
                _logger.LogInformation(
                    $"Updated file '{gitObject.Path}' with contents:{Environment.NewLine}{gitObject.Content}{Environment.NewLine}");
            }

            if (!Options.IsDryRun)
            {
                using IGitHubClient gitHubClient = await _gitHubClientFactory.GetClientAsync(Options.GitOptions, Options.IsDryRun);

                await RetryHelper.GetWaitAndRetryPolicy<HttpRequestException>(_logger).ExecuteAsync(async () =>
                {
                    GitReference gitRef = await GitHelper.PushChangesAsync(gitHubClient, Options, $"Mirroring {productRepo} readmes", branch =>
                    {
                        return FilterUpdatedGitObjectsAsync(gitObjects, gitHubClient, branch);
                    });

                    if (gitRef != null)
                    {
                        _logger.LogInformation(PipelineHelper.FormatOutputVariable("readmeCommitDigest", gitRef.Object.Sha));
                    }
                });
            }
        }

        private bool IncludeReadme(string readmePath) =>
            Options.RootPath is null || Path.GetFullPath(readmePath).StartsWith(Path.GetFullPath(Options.RootPath));

        private void ValidateReadmeFilenames(ManifestInfo manifest)
        {
            // Readme filenames must be unique across all the readmes regardless of their path.
            // This is because they will eventually be published to mcrdocs where all of the readmes are contained within the same directory

            IEnumerable<IGrouping<string, string>> readmePathsWithDuplicateFilenames = manifest.AllRepos
                .SelectMany(repo => repo.Readmes.Select(readme => readme.Path))
                .Where(readmePath => IncludeReadme(readmePath))
                .GroupBy(readmePath => Path.GetFileName(readmePath))
                .Where(group => group.Count() > 1);

            if (readmePathsWithDuplicateFilenames.Any())
            {
                IEnumerable<string> errorMessages = readmePathsWithDuplicateFilenames
                    .Select(group =>
                        "Readme filenames must be unique, regardless of the directory path. " +
                        "The following readme paths have filenames that conflict with each other:" +
                        Environment.NewLine +
                        string.Join(Environment.NewLine, group.ToArray()));

                throw new ValidationException(string.Join(Environment.NewLine + Environment.NewLine, errorMessages.ToArray()));
            }
        }

        private async Task<IEnumerable<GitObject>> FilterUpdatedGitObjectsAsync(
            IEnumerable<GitObject> gitObjects, IGitHubClient gitHubClient, GitHubBranch branch)
        {
            List<GitObject> updatedGitObjects = new();
            foreach (GitObject gitObject in gitObjects)
            {
                string currentContent = await gitHubClient.GetGitHubFileContentsAsync(gitObject.Path, branch);
                if (currentContent == gitObject.Content)
                {
                    _logger.LogInformation($"File '{gitObject.Path}' has not changed.");
                }
                else
                {
                    _logger.LogInformation($"File '{gitObject.Path}' has changed.");
                    updatedGitObjects.Add(gitObject);
                }
            }

            return updatedGitObjects;
        }

        private GitObject GetGitObject(
            string repo,
            string filePath,
            string updatedContent)
        {
            // We only use the filename from the provided file path because all files in the target mcrdocs repo
            // are located at the root of the repo directory.
            string gitPath = string.Join('/', Options.GitOptions.Path, repo, Path.GetFileName(filePath));

            return new GitObject
            {
                Path = gitPath,
                Type = GitObject.TypeBlob,
                Mode = GitObject.ModeFile,
                Content = updatedContent
            };
        }

        private string GetProductRepo()
        {
            string firstRepoName = Manifest.AllRepos.First().QualifiedName
                .TrimStartString($"{Manifest.Registry}/");
            return firstRepoName.Substring(0, firstRepoName.LastIndexOf('/'));
        }

        private GitObject[] GetUpdatedReadmes(string productRepo)
        {
            List<string> readmePaths = Manifest.FilteredRepos
                .SelectMany(repo => repo.Readmes)
                .Select(readme => readme.Path)
                .Where(readmePath => IncludeReadme(readmePath))
                .ToList();

            if (!string.IsNullOrEmpty(Manifest.ReadmePath) && !Options.ExcludeProductFamilyReadme)
            {
                readmePaths.Add(Manifest.ReadmePath);
            }

            List<GitObject> readmes = new();

            foreach (string readmePath in readmePaths)
            {
                string updatedReadMe = File.ReadAllText(readmePath);
                updatedReadMe = ReadmeHelper.UpdateTagsListing(updatedReadMe, McrTagsPlaceholder);
                readmes.Add(GetGitObject(productRepo, readmePath, updatedReadMe));
            }

            return readmes.ToArray();
        }

        private GitObject[] GetUpdatedTagsMetadata(string productRepo)
        {
            List<GitObject> metadata = new();

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                string updatedMetadata = McrTagsMetadataGenerator.Execute(Manifest, repo, generateGitHubLinks: true, _gitService, Options.SourceRepoUrl);
                string metadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplate);
                metadata.Add(GetGitObject(productRepo, metadataFileName, updatedMetadata));
            }

            return metadata.ToArray();
        }
    }
}
