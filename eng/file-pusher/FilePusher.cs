// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FilePusher.Models;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Newtonsoft.Json;

namespace FilePusher
{
    public class FilePusher
    {
        public static Task Main(string[] args)
        {
            RootCommand command = new RootCommand();
            foreach (Symbol symbol in Options.GetCliOptions())
            {
                command.Add(symbol);
            }

            command.Handler = CommandHandler.Create<Options>(ExecuteAsync);

            return command.InvokeAsync(args);
        }

        private static async Task ExecuteAsync(Options options)
        {
            // TODO:  Add support for delete file scenarios

            // Hookup a TraceListener to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            string configJson = File.ReadAllText(options.ConfigPath);
            Config config = JsonConvert.DeserializeObject<Config>(configJson);

            foreach (GitRepo repo in GetFilteredRepos(config, options))
            {
                Console.WriteLine($"Processing {repo.Name}/{repo.Branch}");

                await ExecuteGitOperationsWithRetryAsync(options, async client =>
                {
                    await CreatePullRequestAsync(client, repo, config, options);
                });
            }
        }

        private async static Task AddUpdatedFile(
            List<GitObject> updatedFiles,
            GitHubClient client,
            GitHubBranch branch,
            string filePath,
            string updatedContent)
        {
            if (updatedContent.Contains("\r\n"))
            {
                updatedContent = updatedContent.Replace("\r\n", "\n");
            }

            filePath = filePath.Replace('\\','/');
            string currentContent = await client.GetGitHubFileContentsAsync(filePath, branch);

            if (currentContent == updatedContent)
            {
                Console.WriteLine($"File '{filePath}' has not changed.");
            }
            else
            {
                Console.WriteLine($"File '{filePath}' has changed.");
                updatedFiles.Add(new GitObject
                {
                    Path = filePath,
                    Type = GitObject.TypeBlob,
                    Mode = GitObject.ModeFile,
                    Content = updatedContent
                });
            }
        }

        private async static Task<bool> BranchExists(GitHubClient client, GitHubProject project, string @ref)
        {
            try
            {
                await client.GetReferenceAsync(project, @ref);
                return true;
            }
            catch (HttpFailureResponseException)
            {
                return false;
            }
        }

        private static IEnumerable<GitRepo> GetFilteredRepos(Config config, Options options)
        {
            IEnumerable<GitRepo> activeRepos = config.Repos;
            if (options.Filters?.Any() ?? false)
            {
                string pathsRegexPattern = GetFilterRegexPattern(options.Filters.ToArray());
                activeRepos = activeRepos.Where(repo =>
                    Regex.IsMatch(repo.ToString(), pathsRegexPattern, RegexOptions.IgnoreCase));
            }

            if (!activeRepos.Any())
            {
                Console.WriteLine("No repos found to update.");
                Environment.Exit(1);
            }

            return activeRepos;
        }

        private static async Task CreatePullRequestAsync(GitHubClient client, GitRepo gitRepo, Config config, Options options)
        {
            GitHubProject project = new GitHubProject(gitRepo.Name, gitRepo.Owner);
            GitHubProject forkedProject = new GitHubProject(gitRepo.Name, options.GitUser);
            GitHubBranch baseBranch = new GitHubBranch(gitRepo.Branch, project);
            GitHubBranch headBranch = new GitHubBranch(
                $"{gitRepo.Name}-{gitRepo.Branch}{config.WorkingBranchSuffix}",
                forkedProject);

            IEnumerable<GitObject> changes = await GetUpdatedFiles(config.SourcePath, client, baseBranch);

            if (!changes.Any())
            {
                return;
            }

            GitReference currentRef = await client.GetReferenceAsync(project, $"heads/{baseBranch.Name}");
            string parentSha = currentRef.Object.Sha;
            GitTree tree = await client.PostTreeAsync(forkedProject, parentSha, changes.ToArray());
            GitCommit commit = await client.PostCommitAsync(forkedProject, config.CommitMessage, tree.Sha, new[] { parentSha });

            string workingReference = $"heads/{headBranch.Name}";
            if (await BranchExists(client, forkedProject, workingReference))
            {
                await client.PatchReferenceAsync(forkedProject, workingReference, commit.Sha, force: true);
            }
            else
            {
                await client.PostReferenceAsync(forkedProject, workingReference, commit.Sha);
            }

            GitHubPullRequest pullRequestToUpdate = await client.SearchPullRequestsAsync(
                project,
                headBranch.Name,
                await client.GetMyAuthorIdAsync());

            if (pullRequestToUpdate == null)
            {
                await client.PostGitHubPullRequestAsync(
                    $"[{gitRepo.Branch}] {config.PullRequestTitle}",
                    config.PullRequestDescription,
                    headBranch,
                    baseBranch,
                    maintainersCanModify: true);
            }
        }

        public static async Task ExecuteGitOperationsWithRetryAsync(
            Options options,
            Func<GitHubClient, Task> execute,
            int maxTries = 10,
            int retryMillisecondsDelay = 5000)
        {
            GitHubAuth githubAuth = new GitHubAuth(options.GitAuthToken, options.GitUser, options.GitEmail);
            using (GitHubClient client = new GitHubClient(githubAuth))
            {
                for (int i = 0; i < maxTries; i++)
                {
                    try
                    {
                        await execute(client);

                        break;
                    }
                    catch (HttpRequestException ex) when (i < (maxTries - 1))
                    {
                        Console.WriteLine($"Encountered exception interacting with GitHub: {ex.Message}");
                        Console.WriteLine($"Trying again in {retryMillisecondsDelay}ms. {maxTries - i - 1} tries left.");
                        await Task.Delay(retryMillisecondsDelay);
                    }
                }
            }
        }

        private static IEnumerable<string> GetFiles(string targetDirectory) =>
            Directory.GetDirectories(targetDirectory)
                .SelectMany(dir => GetFiles(dir))
                .Concat(Directory.GetFiles(targetDirectory));

        private static string GetFilterRegexPattern(params string[] patterns)
        {
            string processedPatterns = patterns
                .Select(pattern => Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", "."))
                .Aggregate((working, next) => $"{working}|{next}");
            return $"^({processedPatterns})$";
        }

        private async static Task<GitObject[]> GetUpdatedFiles(string sourcePath, GitHubClient client, GitHubBranch branch)
        {
            List<GitObject> updatedFiles = new List<GitObject>();

            foreach (string file in GetFiles(sourcePath))
            {
                await AddUpdatedFile(updatedFiles, client, branch, file, File.ReadAllText(file));
            }

            return updatedFiles.ToArray();
        }
    }
}
