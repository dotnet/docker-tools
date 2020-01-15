// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IGitHubClientFactory))]
    internal class GitHubClientFactory : IGitHubClientFactory
    {
        public IGitHubClient GetClient(GitHubAuth gitHubAuth, bool isDryRun)
        {
            return new GitHubClientWrapper(new GitHubClient(gitHubAuth), isDryRun);
        }

        // Wrapper class to ensure that no operations with side-effects are invoked when the dry-run option is enabled
        private class GitHubClientWrapper : IGitHubClient
        {
            private readonly GitHubClient innerClient;
            private bool isDryRun;

            public GitHubClientWrapper(GitHubClient innerClient, bool isDryRun)
            {
                this.innerClient = innerClient;
                this.isDryRun = isDryRun;
            }

            public GitHubAuth Auth => this.innerClient.Auth;

            public void Dispose() => this.innerClient.Dispose();

            public void AdjustOptionsToCapability(PullRequestOptions options) =>
                this.innerClient.AdjustOptionsToCapability(options);

            public string CreateGitRemoteUrl(GitHubProject project) =>
                this.innerClient.CreateGitRemoteUrl(project);

            public Task<GitCommit> GetCommitAsync(GitHubProject project, string sha) =>
                this.innerClient.GetCommitAsync(project, sha);

            public Task<GitHubContents> GetGitHubFileAsync(string path, GitHubProject project, string @ref) =>
                this.innerClient.GetGitHubFileAsync(path, project, @ref);

            public Task<string> GetGitHubFileContentsAsync(string path, GitHubBranch branch) =>
                this.innerClient.GetGitHubFileContentsAsync(path, branch);

            public Task<string> GetGitHubFileContentsAsync(string path, GitHubProject project, string @ref) =>
                this.innerClient.GetGitHubFileContentsAsync(path, project, @ref);

            public Task<string> GetMyAuthorIdAsync() =>
                this.innerClient.GetMyAuthorIdAsync();

            public Task<GitReference> GetReferenceAsync(GitHubProject project, string @ref) =>
                this.innerClient.GetReferenceAsync(project, @ref);

            public Task<GitHubCombinedStatus> GetStatusAsync(GitHubProject project, string @ref) =>
                this.innerClient.GetStatusAsync(project, @ref);

            public Task<GitReference> PatchReferenceAsync(GitHubProject project, string @ref, string sha, bool force)
            {
                this.EnsureNotDryRun();
                return this.innerClient.PatchReferenceAsync(project, @ref, sha, force);
            }

            public Task PostCommentAsync(GitHubProject project, int issueNumber, string message)
            {
                this.EnsureNotDryRun();
                return this.innerClient.PostCommentAsync(project, issueNumber, message);
            }

            public Task<GitCommit> PostCommitAsync(GitHubProject project, string message, string tree, string[] parents)
            {
                this.EnsureNotDryRun();
                return this.innerClient.PostCommitAsync(project, message, tree, parents);
            }

            public Task PostGitHubPullRequestAsync(string title, string description, GitHubBranch headBranch, GitHubBranch baseBranch, bool maintainersCanModify)
            {
                this.EnsureNotDryRun();
                return this.innerClient.PostGitHubPullRequestAsync(title, description, headBranch, baseBranch, maintainersCanModify);
            }

            public Task<GitReference> PostReferenceAsync(GitHubProject project, string @ref, string sha)
            {
                this.EnsureNotDryRun();
                return this.innerClient.PostReferenceAsync(project, @ref, sha);
            }

            public Task<GitTree> PostTreeAsync(GitHubProject project, string baseTree, GitObject[] tree)
            {
                this.EnsureNotDryRun();
                return this.innerClient.PostTreeAsync(project, baseTree, tree);
            }

            public Task PutGitHubFileAsync(string fileUrl, string commitMessage, string newFileContents)
            {
                this.EnsureNotDryRun();
                return this.innerClient.PutGitHubFileAsync(fileUrl, commitMessage, newFileContents);
            }

            public Task<GitHubPullRequest> SearchPullRequestsAsync(GitHubProject project, string headPrefix, string author, string sortType = "created") =>
                this.innerClient.SearchPullRequestsAsync(project, headPrefix, author, sortType);

            public Task UpdateGitHubPullRequestAsync(GitHubProject project, int number, string title = null, string body = null, string state = null, bool? maintainersCanModify = null)
            {
                this.EnsureNotDryRun();
                return this.UpdateGitHubPullRequestAsync(project, number, title, body, state, maintainersCanModify);
            }

            private void EnsureNotDryRun()
            {
                if (this.isDryRun)
                {
                    throw new NotSupportedException("This GitHub operation is not supported when the dry-run option is enabled.");
                }
            }
        }
    }
}
