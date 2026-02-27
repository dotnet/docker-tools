// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishImageInfoCommand : ManifestCommand<PublishImageInfoOptions, PublishImageInfoOptionsBuilder>
    {
        private readonly IGitService _gitService;
        private readonly IOctokitClientFactory _octokitClientFactory;
        private readonly ILogger<PublishImageInfoCommand> _logger;
        private const string CommitMessage = "Merging Docker image info updates from build";

        public PublishImageInfoCommand(IManifestJsonService manifestJsonService, IGitService gitService, IOctokitClientFactory octokitClientFactory, ILogger<PublishImageInfoCommand> logger) : base(manifestJsonService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _octokitClientFactory = octokitClientFactory ?? throw new ArgumentNullException(nameof(octokitClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override string Description => "Publishes a build's merged image info.";

        public override async Task ExecuteAsync()
        {
            _logger.LogInformation("PUBLISHING IMAGE INFO");

            string repoPath = Path.Combine(Path.GetTempPath(), "imagebuilder-repos", Options.GitOptions.Repo);
            if (Directory.Exists(repoPath))
            {
                FileHelper.ForceDeleteDirectory(repoPath);
            }

            try
            {
                _logger.LogInformation("Cloning GitHub repo");

                CloneOptions cloneOptions = new() { BranchName = Options.GitOptions.Branch };
                CredentialsHandler credentials = await GetCredentialsAsync();
                cloneOptions.FetchOptions.CredentialsProvider = credentials;

                using IRepository repo =_gitService.CloneRepository(
                    $"https://github.com/{Options.GitOptions.Owner}/{Options.GitOptions.Repo}",
                    repoPath,
                    cloneOptions);

                Uri imageInfoPathIdentifier = GitHelper.GetBlobUrl(Options.GitOptions);

                UpdateGitRepos(repoPath, repo, credentials);
            }
            finally
            {
                if (Directory.Exists(repoPath))
                {
                    FileHelper.ForceDeleteDirectory(repoPath);
                }
            }
        }

        private void UpdateGitRepos(string repoPath, IRepository repo, CredentialsHandler credentials)
        {
            string imageInfoPath = Path.Combine(repoPath, Options.GitOptions.Path);

            // Ensure the directory exists
            string? imageInfoDir = Path.GetDirectoryName(imageInfoPath);
            if (imageInfoDir is not null)
            {
                Directory.CreateDirectory(imageInfoDir);
            }

            File.Copy(Options.ImageInfoPath, imageInfoPath, overwrite: true);

            if (Options.IsDryRun)
            {
                _logger.LogInformation("Skipping commit and push due to dry run.");
                return;
            }

            _gitService.Stage(repo, imageInfoPath);
            Signature sig = new(Options.GitOptions.Username, Options.GitOptions.Email, DateTimeOffset.Now);

            Commit commit;
            try
            {
                _logger.LogInformation("Committing changes...");
                commit = repo.Commit(CommitMessage, sig, sig);
                _logger.LogInformation($"Created commit {commit.Sha}: '{commit.Message}'");
            }
            catch (EmptyCommitException)
            {
                _logger.LogInformation("No changes detected in the image info file. Skipping commit and push.");
                return;
            }

            Branch branch = repo.Branches[Options.GitOptions.Branch];

            _logger.LogInformation("Pushing changes to GitHub");
            repo.Network.Push(branch,
                new PushOptions
                {
                    CredentialsProvider = credentials
                });

            Uri gitHubCommitUrl = GitHelper.GetCommitUrl(Options.GitOptions, commit.Sha);
            _logger.LogInformation(
                $"The '{Options.GitOptions.Path}' file was updated. Remote URL: {gitHubCommitUrl}");
        }

        private async Task<CredentialsHandler> GetCredentialsAsync()
        {
            string token = await _octokitClientFactory.CreateGitHubTokenAsync(Options.GitOptions.GitHubAuthOptions);
            return (_, _, _) => new UsernamePasswordCredentials
            {
                Username = "_",
                Password = token
            };
        }
    }
}
