// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

internal sealed class ModelCommit
{
    private static int s_shaCounter;

    public string Sha { get; } = $"{Interlocked.Increment(ref s_shaCounter):x12}{Guid.NewGuid():N}"[..40];

    public required GitAuthor Author { get; init; }

    public required string Message { get; init; }

    public required ImmutableDictionary<string, string> Tree { get; init; }

    public ImmutableArray<ModelCommit> Parents { get; init; } = [];

    public IEnumerable<ModelCommit> SelfAndAncestors()
    {
        var seen = new HashSet<string>();
        var stack = new Stack<ModelCommit>([this]);
        while (stack.TryPop(out ModelCommit? commit))
        {
            if (seen.Add(commit.Sha))
            {
                yield return commit;
                foreach (ModelCommit parent in commit.Parents)
                {
                    stack.Push(parent);
                }
            }
        }
    }
}

internal enum ModelPullRequestState
{
    Open,
    Closed,
    Merged,
}

internal sealed class ModelPullRequest
{
    public required string HeadBranch { get; init; }

    public required string TargetBranch { get; init; }

    public required string Url { get; init; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public ModelPullRequestState State { get; set; } = ModelPullRequestState.Open;

    public List<string> Comments { get; init; } = [];

    public ModelPullRequest Clone() =>
        new()
        {
            HeadBranch = HeadBranch,
            TargetBranch = TargetBranch,
            Url = Url,
            Title = Title,
            Body = Body,
            State = State,
            Comments = [.. Comments],
        };
}

/// <summary>
/// An in-memory model of a hosted git repository: branches pointing at a
/// commit DAG, plus pull requests with comments. Commits are immutable, so
/// <see cref="Fork"/> is cheap and forked repos share history.
/// </summary>
internal sealed class ModelRepo
{
    public Dictionary<string, ModelCommit> Branches { get; private init; } = [];

    public List<ModelPullRequest> PullRequests { get; private init; } = [];

    public static ModelRepo Create(string branch, GitAuthor author, ImmutableDictionary<string, string> tree)
    {
        var repo = new ModelRepo();
        repo.Branches[branch] = new ModelCommit
        {
            Author = author,
            Message = "Seed repository",
            Tree = tree,
        };
        return repo;
    }

    /// <summary>An independent copy sharing the (immutable) commit history.</summary>
    public ModelRepo Fork() =>
        new()
        {
            Branches = new Dictionary<string, ModelCommit>(Branches),
            PullRequests = [.. PullRequests.Select(pr => pr.Clone())],
        };

    public ModelPullRequest? FindOpenPullRequest(string headBranch) =>
        PullRequests.FirstOrDefault(pr => pr.HeadBranch == headBranch && pr.State == ModelPullRequestState.Open);

    public bool IsReachableFromAnyBranch(string sha) =>
        Branches.Values.Any(tip => tip.SelfAndAncestors().Any(commit => commit.Sha == sha));

    /// <summary>Fast-forward commit on top of the branch's current tip.</summary>
    public ModelCommit Push(string branch, GitAuthor author, string message, ImmutableDictionary<string, string> tree)
    {
        var commit = new ModelCommit
        {
            Author = author,
            Message = message,
            Tree = tree,
            Parents = [Branches[branch]],
        };
        Branches[branch] = commit;
        return commit;
    }

    public void MergePullRequest(ModelPullRequest pullRequest, GitAuthor merger)
    {
        ModelCommit headTip = Branches[pullRequest.HeadBranch];
        ModelCommit targetTip = Branches[pullRequest.TargetBranch];
        Branches[pullRequest.TargetBranch] = new ModelCommit
        {
            Author = merger,
            Message = $"Merge {pullRequest.HeadBranch}",
            Tree = headTip.Tree,
            Parents = [targetTip, headTip],
        };
        pullRequest.State = ModelPullRequestState.Merged;
    }

    /// <summary>
    /// Commits on the head branch that are not reachable from the target
    /// branch and were not authored by <paramref name="automationAuthor"/>.
    /// </summary>
    public IReadOnlyList<ModelCommit> ForeignCommits(string headBranch, string targetBranch, GitAuthor automationAuthor)
    {
        var targetShas = Branches[targetBranch].SelfAndAncestors().Select(c => c.Sha).ToHashSet();
        return
        [
            .. Branches[headBranch]
                .SelfAndAncestors()
                .Where(c => !targetShas.Contains(c.Sha) && c.Author != automationAuthor),
        ];
    }

    /// <summary>
    /// A canonical, SHA-free description of the repo's observable state.
    /// Commit identity is structural (author, message, tree, parents), so two
    /// histories built independently but identically compare equal — which is
    /// what lets properties compare repos across forks and operation orders.
    /// </summary>
    public string Snapshot()
    {
        var sb = new StringBuilder();
        foreach (string name in Branches.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            sb.Append("branch ").Append(name).Append(" = ").AppendLine(Describe(Branches[name]));
        }

        foreach (var group in PullRequests.GroupBy(pr => pr.HeadBranch).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            foreach (ModelPullRequest pr in group)
            {
                sb.Append("pr ")
                    .Append(pr.HeadBranch)
                    .Append(" -> ")
                    .Append(pr.TargetBranch)
                    .Append(" [")
                    .Append(pr.State)
                    .Append("] url=")
                    .Append(pr.Url)
                    .Append(" title=")
                    .Append(pr.Title)
                    .Append(" body=")
                    .Append(pr.Body)
                    .Append(" comments=[")
                    .AppendJoin(" || ", pr.Comments)
                    .AppendLine("]");
            }
        }

        return sb.ToString();
    }

    private static string Describe(ModelCommit commit) =>
        $"({commit.Author.Name}<{commit.Author.Email}>|{commit.Message}"
        + $"|{string.Join(";", commit.Tree.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => $"{kvp.Key}={kvp.Value}"))}"
        + $"|[{string.Join(",", commit.Parents.Select(Describe))}])";
}
