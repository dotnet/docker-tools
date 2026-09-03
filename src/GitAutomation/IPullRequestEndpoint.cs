// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation;

/// <summary>Provides host-specific repository and pull request operations.</summary>
public interface IPullRequestEndpoint
{
    /// <summary>Gets the identity used for commits.</summary>
    /// <param name="cancellationToken">A token that cancels identity acquisition.</param>
    /// <returns>The automation's commit identity.</returns>
    ValueTask<AutomationIdentity> GetIdentityAsync(CancellationToken cancellationToken);

    /// <summary>Clones the pull request source branch.</summary>
    /// <param name="workingCopy">The destination working copy.</param>
    /// <param name="branch">The source branch.</param>
    /// <param name="identity">The identity used for commits.</param>
    /// <param name="cancellationToken">A token that cancels the clone.</param>
    Task CloneSourceAsync(
        GitWorkingCopy workingCopy,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken);

    /// <summary>Clones the pull request target branch.</summary>
    /// <param name="workingCopy">The destination working copy.</param>
    /// <param name="branch">The target branch.</param>
    /// <param name="identity">The identity used for commits.</param>
    /// <param name="cancellationToken">A token that cancels the clone.</param>
    Task CloneTargetAsync(
        GitWorkingCopy workingCopy,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken);

    /// <summary>Pushes the working copy to the pull request source branch.</summary>
    /// <param name="workingCopy">The working copy to push.</param>
    /// <param name="branch">The source branch.</param>
    /// <param name="force">Whether to force-push the branch.</param>
    /// <param name="cancellationToken">A token that cancels the push.</param>
    Task PushAsync(GitWorkingCopy workingCopy, string branch, bool force, CancellationToken cancellationToken);

    /// <summary>Finds the open pull request for a source branch key.</summary>
    /// <param name="key">The source branch key.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>The pull request, or <see langword="null"/> when none exists.</returns>
    Task<ExistingPullRequest?> FindPullRequestAsync(string key, CancellationToken cancellationToken);

    /// <summary>Creates a pull request.</summary>
    /// <param name="pullRequest">The pull request to create.</param>
    /// <param name="cancellationToken">A token that cancels creation.</param>
    /// <returns>The URL of the created pull request.</returns>
    Task<Uri> CreatePullRequestAsync(NewPullRequest pullRequest, CancellationToken cancellationToken);

    /// <summary>Updates an existing pull request.</summary>
    /// <param name="number">The host-assigned pull request number.</param>
    /// <param name="changes">The properties to update.</param>
    /// <param name="cancellationToken">A token that cancels the update.</param>
    Task UpdatePullRequestAsync(int number, PullRequestChanges changes, CancellationToken cancellationToken);
}
