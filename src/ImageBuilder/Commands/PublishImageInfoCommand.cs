// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;


#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishImageInfoCommand : ManifestCommand<PublishImageInfoOptions, PublishImageInfoOptionsBuilder>
    {
        private readonly IGitService _gitService;
        private readonly ILoggerService _loggerService;
        private const string CommitMessage = "Merging Docker image info updates from build";

        [ImportingConstructor]
        public PublishImageInfoCommand(IGitService gitService, ILoggerService loggerService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Publishes a build's merged image info.";

        public override Task ExecuteAsync()
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
                cloneOptions.FetchOptions.CredentialsProvider = (url, user, cred) => new UsernamePasswordCredentials
                {
                    Username = Options.GitOptions.AuthToken,
                    Password = string.Empty
                };

                using IRepository repo = _gitService.CloneRepository(
                    $"https://github.com/{Options.GitOptions.Owner}/{Options.GitOptions.Repo}",
                    repoPath,
                    cloneOptions);

                Uri imageInfoPathIdentifier = GitHelper.GetBlobUrl(Options.GitOptions);

                UpdateGitRepos(repoPath, repo);
            }
            finally
            {
                if (Directory.Exists(repoPath))
                {
                    FileHelper.ForceDeleteDirectory(repoPath);
                }
            }

            return Task.CompletedTask;
        }

        private void UpdateGitRepos(string repoPath, IRepository repo)
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
            Commit commit = repo.Commit(CommitMessage, sig, sig);

            Branch branch = repo.Branches[Options.GitOptions.Branch];

            _loggerService.WriteSubheading("Pushing changes to GitHub");
            repo.Network.Push(branch,
                new PushOptions
                {
                    CredentialsProvider = (url, user, cred) => new UsernamePasswordCredentials
                    {
                        Username = Options.GitOptions.AuthToken,
                        Password = string.Empty
                    }
                });

            Uri gitHubCommitUrl = GitHelper.GetCommitUrl(Options.GitOptions, commit.Sha);
            _loggerService.WriteMessage($"The '{Options.GitOptions.Path}' file was updated: {gitHubCommitUrl}");
        }
    }
}
