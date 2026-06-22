// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Automation;
using Microsoft.DotNet.ImageBuilder.Mcr;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrDocsCommand : ManifestCommand<PublishMcrDocsOptions>
    {
        private const string McrTagsPlaceholder = "Tags go here.";
        private readonly IGitService _gitService;
        private readonly IRepoHostFactory _repoHostFactory;
        private readonly ILogger<PublishMcrDocsCommand> _logger;

        public PublishMcrDocsCommand(IManifestJsonService manifestJsonService, IGitService gitService, IRepoHostFactory repoHostFactory,
            ILogger<PublishMcrDocsCommand> logger) : base(manifestJsonService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _repoHostFactory = repoHostFactory ?? throw new ArgumentNullException(nameof(repoHostFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override string Description => "Publishes the readmes to MCR";

        public override async Task ExecuteAsync()
        {
            _logger.LogInformation("PUBLISHING MCR DOCS");

            ValidateReadmeFilenames(Manifest);

            string productRepo = GetProductRepo();

            IEnumerable<McrDoc> docs =
                GetUpdatedReadmes(productRepo)
                .Concat(GetUpdatedTagsMetadata(productRepo));

            foreach (McrDoc doc in docs)
            {
                _logger.LogInformation(
                    $"Updated file '{doc.Path}' with contents:{Environment.NewLine}{doc.Content}{Environment.NewLine}");
            }

            if (!Options.IsDryRun)
            {
                IRepoHost repoHost =
                    await _repoHostFactory.CreateRepoHostAsync(Options.GitOptions, Options.IsDryRun);

                await RetryHelper.GetWaitAndRetryPolicy<GitException>(_logger).ExecuteAsync(async () =>
                {
                    BranchResult result = await repoHost.EnsureBranchContentAsync(new BranchSpec
                    {
                        Branch = Options.GitOptions.Branch,
                        Apply = async (context, cancellationToken) =>
                        {
                            await WriteDocsAsync(context.Directory, docs);
                            await context.CommitAsync($"Mirroring {productRepo} readmes", cancellationToken);
                        },
                    });

                    GitCommit? lastCommit = result.Commits.LastOrDefault();
                    if (lastCommit is not null)
                    {
                        _logger.LogInformation(PipelineHelper.FormatOutputVariable("readmeCommitDigest", lastCommit.Sha));
                    }
                });
            }
        }

        private static async Task WriteDocsAsync(string repoRoot, IEnumerable<McrDoc> docs)
        {
            foreach (McrDoc doc in docs)
            {
                string path = Path.Combine(repoRoot, doc.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, doc.Content);
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

        private McrDoc GetMcrDoc(
            string repo,
            string filePath,
            string updatedContent)
        {
            // We only use the filename from the provided file path because all files in the target mcrdocs repo
            // are located at the root of the repo directory.
            IEnumerable<string> pathSegments =
                new[] { Options.GitOptions.Path, repo, Path.GetFileName(filePath) }
                    .Where(segment => !string.IsNullOrEmpty(segment));

            return new McrDoc(string.Join('/', pathSegments), updatedContent);
        }

        private string GetProductRepo()
        {
            string firstRepoName = Manifest.AllRepos.First().QualifiedName
                .TrimStartString($"{Manifest.Registry}/");
            return firstRepoName.Substring(0, firstRepoName.LastIndexOf('/'));
        }

        private McrDoc[] GetUpdatedReadmes(string productRepo)
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

            List<McrDoc> readmes = new();

            foreach (string readmePath in readmePaths)
            {
                string updatedReadMe = File.ReadAllText(readmePath);
                updatedReadMe = ReadmeHelper.UpdateTagsListing(updatedReadMe, McrTagsPlaceholder);
                readmes.Add(GetMcrDoc(productRepo, readmePath, updatedReadMe));
            }

            return readmes.ToArray();
        }

        private McrDoc[] GetUpdatedTagsMetadata(string productRepo)
        {
            List<McrDoc> metadata = new();

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                string updatedMetadata = McrTagsMetadataGenerator.Execute(Manifest, repo, generateGitHubLinks: true, _gitService, Options.SourceRepoUrl);
                string metadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplate);
                metadata.Add(GetMcrDoc(productRepo, metadataFileName, updatedMetadata));
            }

            return metadata.ToArray();
        }

        /// <summary>
        /// A doc file to publish, with its path relative to the root of the mcrdocs repo.
        /// </summary>
        private sealed record McrDoc(string Path, string Content);
    }
}
