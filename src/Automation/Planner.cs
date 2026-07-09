// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

internal static class Planner
{
    public static IEnumerable<IOperation> Plan(
        string workspaceDirectory,
        AutomationIdentity identity,
        PullRequestState desiredState,
        TargetBranchState targetBranch,
        ExistingPullRequest? existingPullRequest,
        PullRequestUpdateStrategy updateStrategy,
        ForeignCommitPolicy onForeignCommits
    )
    {
        // Give up without producing any operations when an existing branch contains
        // foreign commits and the policy says to stop.
        if (existingPullRequest is not null
            && onForeignCommits == ForeignCommitPolicy.Stop
            && HasForeignCommits(existingPullRequest, identity))
        {
            return [];
        }

        // The base we compare the desired tree against: the existing pull request's
        // head when one exists, otherwise the target branch we'd branch from.
        string baseTreeHash = existingPullRequest?.Content.TreeHash ?? targetBranch.TreeHash;
        bool hasContentDiff = desiredState.TreeHash != baseTreeHash;

        List<IOperation> operations = [];

        if (hasContentDiff)
        {
            bool forcePush = existingPullRequest is null || updateStrategy == PullRequestUpdateStrategy.Replace;

            operations.Add(new PushCommitsOperation(
                workspaceDirectory,
                desiredState.Key,
                forcePush));
        }

        if (existingPullRequest is null)
        {
            // Only open a pull request when there is actually a diff to propose.
            if (hasContentDiff)
            {
                operations.Add(new CreatePullRequestOperation(
                    Title: desiredState.Title,
                    Body: desiredState.Body,
                    SourceBranch: desiredState.Key,
                    TargetBranch: desiredState.TargetBranch));
            }

            return operations;
        }

        if (desiredState.Title != existingPullRequest.Content.Title)
        {
            UpdateTitleOperation updateTitle = new(existingPullRequest.Number, desiredState.Title);
            operations.Add(updateTitle);
        }

        if (desiredState.Body != existingPullRequest.Content.Body)
        {
            UpdateBodyOperation updateBody = new(existingPullRequest.Number, desiredState.Body);
            operations.Add(updateBody);
        }

        if (desiredState.TargetBranch != existingPullRequest.Content.TargetBranch)
        {
            UpdateBaseBranchOperation updateBase = new(existingPullRequest.Number, desiredState.TargetBranch);
            operations.Add(updateBase);
        }

        return operations;
    }

    private static bool HasForeignCommits(ExistingPullRequest existing, AutomationIdentity identity) =>
        existing.Commits.Any(commit =>
            !string.Equals(commit.AuthorEmail, identity.AuthorEmail, StringComparison.OrdinalIgnoreCase));
}
