// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Defines the desired pull request content and the changes that produce it.
/// </summary>
/// <param name="Key">The source branch name used to identify and update the pull request.</param>
/// <param name="Title">The desired pull request title.</param>
/// <param name="Body">The desired pull request body.</param>
/// <param name="TargetBranch">The branch the pull request targets.</param>
/// <param name="ApplyChanges">Applies the desired workspace changes before the automation commits them.</param>
public sealed partial record PullRequestDefinition(
    string Key,
    string Title,
    string Body,
    string TargetBranch,
    Func<IGitContext, CancellationToken, Task> ApplyChanges)
{
    private string _key = ValidateKey(Key);

    /// <summary>
    /// The source branch name used to identify and update the pull request.
    /// </summary>
    public string Key
    {
        get => _key;
        init => _key = ValidateKey(value);
    }

    private static string ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(Key));

        bool hasValidComponents = key
            .Split('/')
            .All(component =>
                ValidKeyComponentRegex.IsMatch(component)
                && !component.EndsWith(".lock", StringComparison.Ordinal));

        if (key.StartsWith('-') || !hasValidComponents)
        {
            throw new ArgumentException(
                $"'{key}' is not a valid pull request key. Use slash-separated components containing " +
                "ASCII letters, digits, underscores, dashes, and periods. Periods must separate non-empty " +
                "groups, components cannot end in '.lock', and the key cannot start with a dash.",
                nameof(Key));
        }

        return key;
    }

    // Matches one slash-separated component containing ASCII letters, digits, underscores,
    // and dashes, with periods allowed only between non-empty groups.
    [GeneratedRegex(@"^[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidKeyComponentRegex { get; }
}

internal sealed record PullRequestState(string Key, string Title, string Body, string TargetBranch, string TreeHash);

/// <summary>
/// Determines how commits are pushed when updating an existing pull request branch.
/// </summary>
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

/// <summary>
/// Determines how existing pull request commits from other authors are handled.
/// </summary>
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
internal sealed record ExistingPullRequest(PullRequestState Content, int Number, Uri Url, IReadOnlyList<CommitInfo> Commits);

/// <summary>
/// The observed state of the branch a new pull request would be created from.
/// When no pull request exists yet, its tree is the base we diff the desired
/// tree against to decide whether there is anything to propose.
/// </summary>
internal sealed record TargetBranchState(string TreeHash);

/// <summary>
/// The automation's git identity, used to distinguish its own commits from foreign ones.
/// </summary>
/// <param name="AuthorName">The git author name used for automation commits.</param>
/// <param name="AuthorEmail">The git author email used for automation commits.</param>
public sealed record AutomationIdentity(string AuthorName, string AuthorEmail);

/// <summary>
/// A single commit observed on an existing pull request's branch.
/// </summary>
internal sealed record CommitInfo(string Sha, string AuthorName, string AuthorEmail);

// TODO: Use C# 15 unions after .NET 11's release
internal interface IOperation;
internal sealed record PushCommitsOperation(string WorkspaceDirectory, string SourceBranch, bool ForcePush) : IOperation;
internal sealed record CreatePullRequestOperation(string Title, string Body, string SourceBranch, string TargetBranch) : IOperation;
internal sealed record UpdateTitleOperation(int Number, string Title) : IOperation;
internal sealed record UpdateBodyOperation(int Number, string Body) : IOperation;
internal sealed record UpdateBaseBranchOperation(int Number, string TargetBranch) : IOperation;

// TODO: Use C# 15 unions after .NET 11's release
internal interface IOperationResult;
internal sealed record CommitsPushed(string Branch, string FromSha, string ToSha, Uri Url) : IOperationResult;
internal sealed record PullRequestCreated(int Number, Uri Url) : IOperationResult;
internal sealed record TitleUpdated(int Number, string Title) : IOperationResult;
internal sealed record BodyUpdated(int Number, string Body) : IOperationResult;
internal sealed record BaseBranchUpdated(int Number, string TargetBranch) : IOperationResult;
