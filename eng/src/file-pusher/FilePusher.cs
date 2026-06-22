// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FilePusher.Models;
using Microsoft.DotNet.Automation;
using Microsoft.DotNet.Automation.GitHub;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FilePusher
{
    public class FilePusher
    {
        public static Task Main(string[] args)
        {
            RootCommand command = [.. Options.GetCliOptions()];
            command.Handler = CommandHandler.Create<Options>(ExecuteAsync);
            return command.InvokeAsync(args);
        }

        private static async Task ExecuteAsync(Options options)
        {
            try
            {
                string configJson = File.ReadAllText(options.ConfigPath);
                Config config = JsonConvert.DeserializeObject<Config>(configJson)
                    ?? throw new ArgumentException($"Could not serialize config JSON file {options.ConfigPath}.");

                await PushFilesAsync(options, config);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to push files:{Environment.NewLine}{e}");
                Environment.Exit(1);
            }
        }

        public static async Task PushFilesAsync(Options options, Config config)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());

            GitAutomationOptions gitOptions = new(
                options.GitAuthToken,
                new GitAuthor(options.GitUser, options.GitEmail));

            foreach (GitRepo repo in GetFilteredRepos(config, options))
            {
                Console.WriteLine($"Processing {repo.Name}/{repo.Branch}");

                IRepoHost repoHost = new GitHubRepoHost(
                    repo: new GitHubRepo(repo.Owner, repo.Name),
                    options: gitOptions,
                    headRepo: new GitHubRepo(options.GitUser, repo.Name),
                    loggerFactory: loggerFactory);

                PullRequestSpec spec = new()
                {
                    Key = $"{repo.Name}-{repo.Branch}{config.WorkingBranchSuffix}",
                    Title = $"[{repo.Branch}] {config.PullRequestTitle}",
                    Body = config.PullRequestDescription ?? string.Empty,
                    TargetBranch = repo.Branch,
                    // Stack each run's changes as a new commit on top of whatever is
                    // already on the branch instead of force-pushing, so we never
                    // discard commits another actor pushed to the bot's branch.
                    UpdateStrategy = PullRequestUpdateStrategy.Append,
                    OnForeignCommits = ForeignCommitPolicy.Proceed,
                    Apply = async (context, cancellationToken) =>
                    {
                        await CopyFilesAsync(config.SourcePath, context.Directory);
                        await context.CommitAsync(config.CommitMessage, cancellationToken);
                    },
                };

                await RetryAsync(() => repoHost.EnsurePullRequestAsync(spec));
            }
        }

        /// <summary>
        /// Copies all files under <paramref name="sourcePath"/> into the repo,
        /// preserving their relative paths.
        /// </summary>
        private static async Task CopyFilesAsync(string sourcePath, string repoRoot)
        {
            foreach (string file in GetFiles(sourcePath))
            {
                string content = await File.ReadAllTextAsync(file);
                content = content.Replace("\r\n", "\n");

                string destinationPath = Path.Combine(repoRoot, file.Replace('\\', '/'));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await File.WriteAllTextAsync(destinationPath, content);
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

        private static async Task RetryAsync(
            Func<Task> execute,
            int maxTries = 10,
            int retryMillisecondsDelay = 5000)
        {
            for (int i = 0; i < maxTries; i++)
            {
                try
                {
                    await execute();
                    break;
                }
                catch (Exception ex) when (ex is HttpRequestException or GitException && i < (maxTries - 1))
                {
                    Console.WriteLine($"Encountered exception interacting with GitHub: {ex.Message}");
                    Console.WriteLine($"Trying again in {retryMillisecondsDelay}ms. {maxTries - i - 1} tries left.");
                    await Task.Delay(retryMillisecondsDelay);
                }
            }
        }

        private static IEnumerable<string> GetFiles(string path)
        {
            if (File.Exists(path))
            {
                return new string[] { path };
            }
            else
            {
                return Directory.GetDirectories(path)
                    .SelectMany(dir => GetFiles(dir))
                    .Concat(Directory.GetFiles(path));
            }
        }

        private static string GetFilterRegexPattern(params string[] patterns)
        {
            string processedPatterns = patterns
                .Select(pattern => Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", "."))
                .Aggregate((working, next) => $"{working}|{next}");
            return $"^({processedPatterns})$";
        }
    }
}
