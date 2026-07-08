// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

public sealed record PullRequestDefinition(
    string Key,
    string Title,
    string Body,
    string TargetBranch,
    Func<IGitContext, CancellationToken, Task> ApplyChanges);

public sealed record PullRequestState(string Key, string Title, string Body, string TargetBranch, string TreeHash);

public enum PullRequestUpdateStrategy
{
    /// <summary>
    /// Add the automation's new commits on top of the branch's existing commits without force-pushing.
    /// </summary>
    Append,

    /// <summary>
    /// Overwrite the branch with exactly the automation's commits by force-pushing.
    /// </summary>
    Replace,
}

public enum ForeignCommitPolicy
{
    /// <summary>
    /// Apply the update strategy regardless of who authored the branch's existing commits.
    /// </summary>
    Proceed,

    /// <summary>
    /// Give up without modifying the branch if it contains commits not authored by the automation.
    /// </summary>
    Stop,
}

/// <summary>
/// The action a <see cref="PullRequestManager"/> took to reconcile a pull request.
/// </summary>
public enum PullRequestAction
{
    /// <summary>
    /// A new pull request was opened.
    /// </summary>
    Created,

    /// <summary>
    /// An existing pull request was updated (commits pushed and/or metadata changed).
    /// </summary>
    Updated,

    /// <summary>
    /// The pull request already matched the definition, so nothing was changed.
    /// </summary>
    NoChange,
}

/// <summary>
/// The result of a pull request automation.
/// </summary>
/// <param name="Action">What action was taken.</param>
/// <param name="Url">
/// The URL of the pull request if one was created or already exists.
/// Null if one didn't already exist and no action was needed.
/// </param>
public sealed record PullRequestResult(PullRequestAction Action, Uri? Url);

/// <summary>
/// An existing pull request as observed on the host: its <see cref="Content"/> plus
/// host-assigned facts that only exist once it has been opened. <see cref="Url"/> is an
/// output-only convenience for callers; the planner deliberately ignores it so it can
/// never influence planning.
/// </summary>
public sealed record ExistingPullRequest(PullRequestState Content, int Number, Uri Url, IReadOnlyList<CommitInfo> Commits);

/// <summary>
/// The observed state of the branch a new pull request would be created from.
/// When no pull request exists yet, its tree is the base we diff the desired
/// tree against to decide whether there is anything to propose.
/// </summary>
public sealed record TargetBranchState(string TreeHash);

/// <summary>
/// The automation's git identity, used to distinguish its own commits from foreign ones.
/// </summary>
public sealed record AutomationIdentity(string AuthorName, string AuthorEmail);

/// <summary>
/// A single commit observed on an existing pull request's branch.
/// </summary>
public sealed record CommitInfo(string Sha, string AuthorName, string AuthorEmail);

// TODO: Use C# 15 unions after .NET 11's release
public interface IOperation;
public sealed record PushCommitsOperation(string WorkspaceDirectory, string SourceBranch, bool ForcePush) : IOperation;
public sealed record CreatePullRequestOperation(string Title, string Body, string SourceBranch, string TargetBranch) : IOperation;
public sealed record UpdateTitleOperation(int Number, string Title) : IOperation;
public sealed record UpdateBodyOperation(int Number, string Body) : IOperation;
public sealed record UpdateBaseBranchOperation(int Number, string TargetBranch) : IOperation;

// TODO: Use C# 15 unions after .NET 11's release
public interface IOperationResult;
public sealed record CommitsPushed(string Branch, string FromSha, string ToSha, Uri Url) : IOperationResult;
public sealed record PullRequestCreated(int Number, Uri Url) : IOperationResult;
public sealed record TitleUpdated(int Number, string Title) : IOperationResult;
public sealed record BodyUpdated(int Number, string Body) : IOperationResult;
public sealed record BaseBranchUpdated(int Number, string TargetBranch) : IOperationResult;
