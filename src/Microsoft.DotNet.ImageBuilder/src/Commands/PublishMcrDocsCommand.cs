// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishMcrDocsCommand : ManifestCommand<PublishMcrDocsOptions, PublishMcrDocsOptionsBuilder>
    {
        private const string McrTagsPlaceholder = "Tags go here.";
        private readonly IGitService _gitService;
        private readonly IGitHubClientFactory _gitHubClientFactory;
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public PublishMcrDocsCommand(IGitService gitService, IGitHubClientFactory gitHubClientFactory,
            ILoggerService loggerService) : base()
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Publishes the readmes to MCR";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("PUBLISHING MCR DOCS");

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            IEnumerable<GitObject> gitObjects =
                GetUpdatedReadmes()
                .Concat(GetUpdatedTagsMetadata());

            foreach (GitObject gitObject in gitObjects)
            {
                _loggerService.WriteMessage(
                    $"Updated file '{gitObject.Path}' with contents:{Environment.NewLine}{gitObject.Content}{Environment.NewLine}");
            }

            if (!Options.IsDryRun)
            {
                using IGitHubClient gitHubClient = _gitHubClientFactory.GetClient(Options.GitOptions.ToGitHubAuth(), Options.IsDryRun);

                await GitHelper.ExecuteGitOperationsWithRetryAsync(async () =>
                {
                    GitReference gitRef = await GitHelper.PushChangesAsync(gitHubClient, Options, $"Mirroring readmes", branch =>
                    {
                        return FilterUpdatedGitObjectsAsync(gitObjects, gitHubClient, branch);
                    });

                    if (gitRef != null)
                    {
                        _loggerService.WriteMessage(PipelineHelper.FormatOutputVariable("readmeCommitDigest", gitRef.Object.Sha));
                    }
                });
            }
        }

        private async Task<IEnumerable<GitObject>> FilterUpdatedGitObjectsAsync(
            IEnumerable<GitObject> gitObjects, IGitHubClient gitHubClient, GitHubBranch branch)
        {
            List<GitObject> updatedGitObjects = new List<GitObject>();
            foreach (GitObject gitObject in gitObjects)
            {
                string currentContent = await gitHubClient.GetGitHubFileContentsAsync(gitObject.Path, branch);
                if (currentContent == gitObject.Content)
                {
                    _loggerService.WriteMessage($"File '{gitObject.Path}' has not changed.");
                }
                else
                {
                    _loggerService.WriteMessage($"File '{gitObject.Path}' has changed.");
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
            string gitPath = string.Join('/', Options.GitOptions.Path, repo, filePath);

            return new GitObject
            {
                Path = gitPath,
                Type = GitObject.TypeBlob,
                Mode = GitObject.ModeFile,
                Content = updatedContent
            };
        }

        private static string GetProductRepoName(RepoInfo repo)
        {
            return repo.Name.Substring(0, repo.Name.LastIndexOf('/'));
        }

        private GitObject[] GetUpdatedReadmes()
        {
            List<GitObject> readmes = new List<GitObject>();

            if (!string.IsNullOrEmpty(Manifest.ReadmePath))
            {
                IEnumerable<string> productRepoNames = Manifest.FilteredRepos
                    .Select(repo => GetProductRepoName(repo))
                    .Distinct();
                foreach (string productRepo in productRepoNames)
                {
                    readmes.Add(GetReadMeGitObject(productRepo, Manifest.ReadmePath, containsTagListing: false));
                }
            }

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                readmes.Add(GetReadMeGitObject(GetProductRepoName(repo), repo.ReadmePath, containsTagListing: true));
            }

            return readmes.ToArray();
        }

        private GitObject GetReadMeGitObject(string productRepoName, string readmePath, bool containsTagListing)
        {
            string updatedReadMe = File.ReadAllText(readmePath);
            if (containsTagListing)
            {
                updatedReadMe = ReadmeHelper.UpdateTagsListing(updatedReadMe, McrTagsPlaceholder);
            }
            
            return GetGitObject(productRepoName, readmePath, updatedReadMe);
        }

        private GitObject[] GetUpdatedTagsMetadata()
        {
            List<GitObject> metadata = new List<GitObject>();

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                string updatedMetadata = McrTagsMetadataGenerator.Execute(_gitService, Manifest, repo, Options.SourceRepoUrl);
                string metadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplate);
                metadata.Add(GetGitObject(GetProductRepoName(repo), metadataFileName, updatedMetadata));
            }

            return metadata.ToArray();
        }
    }
}
