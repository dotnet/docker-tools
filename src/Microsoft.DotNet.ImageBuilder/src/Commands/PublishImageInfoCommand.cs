// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishImageInfoCommand : ManifestCommand<PublishImageInfoOptions, PublishImageInfoOptionsBuilder>
    {
        private readonly IGitService _gitService;
        private readonly IOctokitClientFactory _octokitClientFactory;
        private readonly ILoggerService _loggerService;
        private const string CommitMessage = "Merging Docker image info updates from build";

        [ImportingConstructor]
        public PublishImageInfoCommand(IGitService gitService, IOctokitClientFactory octokitClientFactory, ILoggerService loggerService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _octokitClientFactory = octokitClientFactory ?? throw new ArgumentNullException(nameof(octokitClientFactory));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Publishes a build's merged image info.";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("PUBLISHING IMAGE INFO");

            string repoPath = Path.Combine(Path.GetTempPath(), "imagebuilder-repos", Options.GitOptions.Repo);
            if (Directory.Exists(repoPath))
            {
                FileHelper.ForceDeleteDirectory(repoPath);
            }

            try
            {
                _loggerService.WriteSubheading("Cloning GitHub repo");

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
                return;
            }

            _gitService.Stage(repo, imageInfoPath);
            Signature sig = new(Options.GitOptions.Username, Options.GitOptions.Email, DateTimeOffset.Now);

            Commit commit;
            try
            {
                commit = repo.Commit(CommitMessage, sig, sig);
            }
            catch (EmptyCommitException)
            {
                _loggerService.WriteMessage("No changes detected in the image info file. Skipping commit and push.");
                return;
            }

            Branch branch = repo.Branches[Options.GitOptions.Branch];

            _loggerService.WriteSubheading("Pushing changes to GitHub");
            repo.Network.Push(branch,
                new PushOptions
                {
                    CredentialsProvider = credentials
                });

            Uri gitHubCommitUrl = GitHelper.GetCommitUrl(Options.GitOptions, commit.Sha);
            _loggerService.WriteMessage($"The '{Options.GitOptions.Path}' file was updated: {gitHubCommitUrl}");
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
