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
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrDocsCommand : Command<PublishMcrDocsOptions>
    {
        private const int MaxTries = 10;
        private const string McrTagsPlaceholder = "Tags go here.";
        private const int RetryMillisecondsDelay = 5000;

        public PublishMcrDocsCommand() : base()
        {
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("PUBLISHING MCR DOCS");

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            string productRepo = GetProductRepo();

            GitHubAuth githubAuth = new GitHubAuth(Options.GitAuthToken, Options.GitUsername, Options.GitEmail);
            using (GitHubClient client = new GitHubClient(githubAuth))
            {
                for (int i = 0; i < MaxTries; i++)
                {
                    try
                    {
                        GitHubProject project = new GitHubProject(Options.GitRepo, Options.GitOwner);
                        GitHubBranch branch = new GitHubBranch(Options.GitBranch, project);
                        GitObject[] gitObjects = (await GetUpdatedReadmes(productRepo, client, branch))
                            .Concat(await GetUpdatedTagsMetadata(productRepo, client, branch))
                            .ToArray();

                        if (gitObjects.Any())
                        {
                            string masterRef = $"heads/{Options.GitBranch}";
                            GitReference currentMaster = await client.GetReferenceAsync(project, masterRef);
                            string masterSha = currentMaster.Object.Sha;
                            GitTree tree = await client.PostTreeAsync(project, masterSha, gitObjects);
                            string commitMessage = $"Mirroring {productRepo} readmes";
                            GitCommit commit = await client.PostCommitAsync(
                                project, commitMessage, tree.Sha, new[] { masterSha });

                            // Only fast-forward. Don't overwrite other changes: throw exception instead.
                            await client.PatchReferenceAsync(project, masterRef, commit.Sha, force: false);
                        }

                        break;
                    }
                    catch (HttpRequestException ex) when (i < (MaxTries - 1))
                    {
                        Logger.WriteMessage($"Encountered exception publishing readmes: {ex.Message}");
                        Logger.WriteMessage($"Trying again in {RetryMillisecondsDelay}ms. {MaxTries - i - 1} tries left.");
                        await Task.Delay(RetryMillisecondsDelay);
                    }
                }
            }
        }

        private async Task AddUpdatedFile(
            List<GitObject> updatedFiles,
            GitHubClient client,
            GitHubBranch branch,
            string repo,
            string filePath,
            string updatedContent)
        {
            string gitPath = string.Join('/', Options.GitPath, repo, filePath);
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

        private async Task<GitObject[]> GetUpdatedReadmes(string productRepo, GitHubClient client, GitHubBranch branch)
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
                string updatedReadMe = File.ReadAllText(readmePath);
                updatedReadMe = ReadmeHelper.UpdateTagsListing(updatedReadMe, McrTagsPlaceholder);
                await AddUpdatedFile(readmes, client, branch, productRepo, readmePath, updatedReadMe);
            }

            return readmes.ToArray();
        }

        private async Task<GitObject[]> GetUpdatedTagsMetadata(string productRepo, GitHubClient client, GitHubBranch branch)
        {
            List<GitObject> metadata = new List<GitObject>();

            foreach (RepoInfo repo in Manifest.FilteredRepos)
            {
                string updatedMetadata = McrTagsMetadataGenerator.Execute(Manifest, repo, Options.SourceUrl);
                string metadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplatePath);
                await AddUpdatedFile(metadata, client, branch, productRepo, metadataFileName, updatedMetadata);
            }

            return metadata.ToArray();
        }
    }
}
