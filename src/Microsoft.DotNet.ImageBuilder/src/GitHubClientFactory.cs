// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
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
            private readonly GitHubClient _innerClient;
            private readonly bool _isDryRun;

            public GitHubClientWrapper(GitHubClient innerClient, bool isDryRun)
            {
                _innerClient = innerClient;
                _isDryRun = isDryRun;
            }

            public GitHubAuth Auth => _innerClient.Auth;

            public void Dispose() => _innerClient.Dispose();

            public void AdjustOptionsToCapability(PullRequestOptions options) =>
                _innerClient.AdjustOptionsToCapability(options);

            public string CreateGitRemoteUrl(GitHubProject project) =>
                _innerClient.CreateGitRemoteUrl(project);

            public Task<GitCommit> GetCommitAsync(GitHubProject project, string sha) =>
                _innerClient.GetCommitAsync(project, sha);

            public Task<GitHubContents> GetGitHubFileAsync(string path, GitHubProject project, string @ref) =>
                _innerClient.GetGitHubFileAsync(path, project, @ref);

            public Task<string> GetGitHubFileContentsAsync(string path, GitHubBranch branch) =>
                _innerClient.GetGitHubFileContentsAsync(path, branch);

            public Task<string> GetGitHubFileContentsAsync(string path, GitHubProject project, string @ref) =>
                _innerClient.GetGitHubFileContentsAsync(path, project, @ref);

            public Task<string> GetMyAuthorIdAsync() =>
                _innerClient.GetMyAuthorIdAsync();

            public Task<GitReference> GetReferenceAsync(GitHubProject project, string @ref) =>
                _innerClient.GetReferenceAsync(project, @ref);

            public Task<GitHubCombinedStatus> GetStatusAsync(GitHubProject project, string @ref) =>
                _innerClient.GetStatusAsync(project, @ref);

            public Task<GitReference> PatchReferenceAsync(GitHubProject project, string @ref, string sha, bool force)
            {
                EnsureNotDryRun();
                return _innerClient.PatchReferenceAsync(project, @ref, sha, force);
            }

            public Task PostCommentAsync(GitHubProject project, int issueNumber, string message)
            {
                EnsureNotDryRun();
                return _innerClient.PostCommentAsync(project, issueNumber, message);
            }

            public Task<GitCommit> PostCommitAsync(GitHubProject project, string message, string tree, string[] parents)
            {
                EnsureNotDryRun();
                return _innerClient.PostCommitAsync(project, message, tree, parents);
            }

            public Task PostGitHubPullRequestAsync(string title, string description, GitHubBranch headBranch, GitHubBranch baseBranch, bool maintainersCanModify)
            {
                EnsureNotDryRun();
                return _innerClient.PostGitHubPullRequestAsync(title, description, headBranch, baseBranch, maintainersCanModify);
            }

            public Task<GitReference> PostReferenceAsync(GitHubProject project, string @ref, string sha)
            {
                EnsureNotDryRun();
                return _innerClient.PostReferenceAsync(project, @ref, sha);
            }

            public Task<GitTree> PostTreeAsync(GitHubProject project, string baseTree, GitObject[] tree)
            {
                EnsureNotDryRun();
                return _innerClient.PostTreeAsync(project, baseTree, tree);
            }

            public Task PutGitHubFileAsync(string fileUrl, string commitMessage, string newFileContents)
            {
                EnsureNotDryRun();
                return _innerClient.PutGitHubFileAsync(fileUrl, commitMessage, newFileContents);
            }

            public Task<GitHubPullRequest> SearchPullRequestsAsync(GitHubProject project, string headPrefix, string author, string sortType = "created") =>
                _innerClient.SearchPullRequestsAsync(project, headPrefix, author, sortType);

            public Task UpdateGitHubPullRequestAsync(GitHubProject project, int number, string title = null, string body = null, string state = null, bool? maintainersCanModify = null)
            {
                EnsureNotDryRun();
                return UpdateGitHubPullRequestAsync(project, number, title, body, state, maintainersCanModify);
            }

            private void EnsureNotDryRun()
            {
                if (_isDryRun)
                {
                    throw new NotSupportedException("This GitHub operation is not supported when the dry-run option is enabled.");
                }
            }
        }
    }
}
