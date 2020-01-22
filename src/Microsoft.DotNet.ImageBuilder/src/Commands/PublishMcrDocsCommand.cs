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

        [ImportingConstructor]
        public PublishMcrDocsCommand(IGitService gitService, IGitHubClientFactory gitHubClientFactory) : base()
        {
            this.gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            this.gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("PUBLISHING MCR DOCS");

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            string productRepo = GetProductRepo();

            using IGitHubClient gitHubClient = this.gitHubClientFactory.GetClient(Options.GitOptions.ToGitHubAuth(), Options.IsDryRun);

            await GitHelper.ExecuteGitOperationsWithRetryAsync(async () =>
            {
                await GitHelper.PushChangesAsync(gitHubClient, Options, $"Mirroring {productRepo} readmes", async branch =>
                {
                    return (await GetUpdatedReadmes(productRepo, gitHubClient, branch))
                        .Concat(await GetUpdatedTagsMetadata(productRepo, gitHubClient, branch));
                });
            });
        }

        private async Task AddUpdatedFile(
            List<GitObject> updatedFiles,
            IGitHubClient client,
            GitHubBranch branch,
            string repo,
            string filePath,
            string updatedContent)
        {
            string gitPath = string.Join('/', Options.GitOptions.Path, repo, filePath);
            string currentContent = await client.GetGitHubFileContentsAsync(gitPath, branch);

            if (currentContent == updatedContent)
            {
                Logger.WriteMessage($"File '{filePath}' has not changed.");
            }
            else
            {
                Logger.WriteMessage($"File '{filePath}' has changed.");
                updatedFiles.Add(new GitObject
                {
                    Path = gitPath,
                    Type = GitObject.TypeBlob,
                    Mode = GitObject.ModeFile,
                    Content = updatedContent
                });
            }
        }

        private string GetProductRepo()
        {
            string firstRepoName = Manifest.AllRepos.First().Name
                .TrimStart($"{Manifest.Registry}/");
            return firstRepoName.Substring(0, firstRepoName.LastIndexOf('/'));
        }

        private async Task<GitObject[]> GetUpdatedReadmes(string productRepo, IGitHubClient client, GitHubBranch branch)
        {
            List<GitObject> readmes = new List<GitObject>();

            List<string> readmePaths = Manifest.FilteredRepos
                .Select(repo => repo.Model.ReadmePath)
                .ToList();

            if (!string.IsNullOrEmpty(Manifest.Model.ReadmePath))
            {
                readmePaths.Add(Manifest.Model.ReadmePath);
            }

            foreach (string readmePath in readmePaths)
            {
                string fullPath = Path.IsPathRooted(readmePath) ?
                    readmePath : Path.Combine(Manifest.Directory, readmePath);
                
                string updatedReadMe = File.ReadAllText(fullPath);
                updatedReadMe = ReadmeHelper.UpdateTagsListing(updatedReadMe, McrTagsPlaceholder);
                await AddUpdatedFile(readmes, client, branch, productRepo, fullPath, updatedReadMe);
            }

            return readmes.ToArray();
        }

        private async Task<GitObject[]> GetUpdatedTagsMetadata(string productRepo, IGitHubClient client, GitHubBranch branch)
        {
            List<GitObject> metadata = new List<GitObject>();

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                string updatedMetadata = McrTagsMetadataGenerator.Execute(this.gitService, Manifest, repo, Options.SourceRepoUrl);
                string metadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplatePath);
                await AddUpdatedFile(metadata, client, branch, productRepo, metadataFileName, updatedMetadata);
            }

            return metadata.ToArray();
        }
    }
}
