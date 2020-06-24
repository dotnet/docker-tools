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
    public class PublishMcrDocsCommand : ManifestCommand<PublishMcrDocsOptions>
    {
        private const string McrTagsPlaceholder = "Tags go here.";
        private readonly IGitService gitService;
        private readonly IGitHubClientFactory gitHubClientFactory;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public PublishMcrDocsCommand(IGitService gitService, IGitHubClientFactory gitHubClientFactory,
            ILoggerService loggerService) : base()
        {
            this.gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            this.gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public override async Task ExecuteAsync()
        {
            loggerService.WriteHeading("PUBLISHING MCR DOCS");

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            IEnumerable<GitObject> gitObjects =
                GetUpdatedReadmes()
                .Concat(GetUpdatedTagsMetadata());

            foreach (GitObject gitObject in gitObjects)
            {
                this.loggerService.WriteMessage(
                    $"Updated file '{gitObject.Path}' with contents:{Environment.NewLine}{gitObject.Content}{Environment.NewLine}");
            }

            if (!Options.IsDryRun)
            {
                using IGitHubClient gitHubClient = this.gitHubClientFactory.GetClient(Options.GitOptions.ToGitHubAuth(), Options.IsDryRun);

                await GitHelper.ExecuteGitOperationsWithRetryAsync(async () =>
                {
                    GitReference gitRef = await GitHelper.PushChangesAsync(gitHubClient, Options, $"Mirroring readmes", branch =>
                    {
                        return FilterUpdatedGitObjectsAsync(gitObjects, gitHubClient, branch);
                    });

                    if (gitRef != null)
                    {
                        this.loggerService.WriteMessage(PipelineHelper.FormatOutputVariable("readmeCommitDigest", gitRef.Object.Sha));
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
                    this.loggerService.WriteMessage($"File '{gitObject.Path}' has not changed.");
                }
                else
                {
                    this.loggerService.WriteMessage($"File '{gitObject.Path}' has changed.");
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

            if (!string.IsNullOrEmpty(Manifest.Model.ReadmePath))
            {
                IEnumerable<string> productRepoNames = Manifest.FilteredRepos
                    .Select(repo => GetProductRepoName(repo))
                    .Distinct();
                foreach (string productRepo in productRepoNames)
                {
                    readmes.Add(GetReadMeGitObject(productRepo, Manifest.Model.ReadmePath));
                }
            }

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                readmes.Add(GetReadMeGitObject(GetProductRepoName(repo), repo.Model.ReadmePath));
            }

            return readmes.ToArray();
        }

        private GitObject GetReadMeGitObject(string productRepoName, string readmePath)
        {
            string fullPath = Path.Combine(Manifest.Directory, readmePath);
            string updatedReadMe = File.ReadAllText(fullPath);
            updatedReadMe = ReadmeHelper.UpdateTagsListing(updatedReadMe, McrTagsPlaceholder);
            return GetGitObject(productRepoName, fullPath, updatedReadMe);
        }

        private GitObject[] GetUpdatedTagsMetadata()
        {
            List<GitObject> metadata = new List<GitObject>();

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                string updatedMetadata = McrTagsMetadataGenerator.Execute(this.gitService, Manifest, repo, Options.SourceRepoUrl);
                string metadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplatePath);
                metadata.Add(GetGitObject(GetProductRepoName(repo), metadataFileName, updatedMetadata));
            }

            return metadata.ToArray();
        }
    }
}
