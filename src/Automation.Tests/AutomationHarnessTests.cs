// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.Tests;

/// <summary>
/// Representative tests that double as examples of how to use the harness:
/// set up a state of the world, take an action in the library, then assert on
/// what the library did.
/// </summary>
[TestClass]
public sealed class AutomationHarnessTests
{
    private static readonly GitAuthor s_foreignAuthor = new("other-contributor", "other@example.test");

    [TestMethod]
    public async Task EnsurePullRequest_NoExistingPullRequest_CreatesPullRequest()
    {
        using AutomationHarness harness = await AutomationHarness.CreateAsync();

        PullRequestSpec spec = new()
        {
            Key = "update-tools",
            Title = "Update tools",
            Body = "Automated update.",
            TargetBranch = "main",
            Apply = WriteFile("tools.txt", "v1", "Add tools"),
        };

        PullRequestResult result = await harness.EnsurePullRequestAsync(spec);

        result.Outcome.ShouldBe(PullRequestOutcome.Created);
        result.Commits.Select(commit => commit.Message).ShouldBe(["Add tools"]);

        // The library opened exactly one pull request with the requested metadata.
        harness.PullRequests.Creates.Count.ShouldBe(1);
        RecordedCreate created = harness.PullRequests.Creates[0];
        created.Title.ShouldBe("Update tools");
        created.Body.ShouldBe("Automated update.");
        created.HeadBranch.ShouldBe("update-tools");
        created.TargetBranch.ShouldBe("main");

        // The head branch was pushed to the remote with the applied content.
        (await harness.Head.BranchExistsAsync("update-tools")).ShouldBeTrue();
        (await harness.Head.GetFileAtRefAsync("update-tools", "tools.txt")).ShouldBe("v1");
    }

    [TestMethod]
    public async Task EnsurePullRequest_BranchAlreadyMatches_IsNoOp()
    {
        using AutomationHarness harness = await AutomationHarness.CreateAsync();

        // State of the world: an open pull request whose branch already has the desired content.
        await harness.Head.SeedBranchAsync(
            branch: "update-tools",
            fromBranch: "main",
            relativePath: "tools.txt",
            content: "v1",
            author: AutomationHarness.DefaultAuthor,
            message: "Add tools");
        harness.PullRequests.SeedOpenPullRequest("update-tools", "main", "Update tools", "Automated update.");

        PullRequestSpec spec = new()
        {
            Key = "update-tools",
            Title = "Update tools",
            Body = "Automated update.",
            TargetBranch = "main",
            Apply = WriteFile("tools.txt", "v1", "Add tools"),
        };

        PullRequestResult result = await harness.EnsurePullRequestAsync(spec);

        result.Outcome.ShouldBe(PullRequestOutcome.Unchanged);
        harness.PullRequests.Creates.ShouldBeEmpty();
        harness.PullRequests.Updates.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task EnsurePullRequest_ForeignCommitOnBranch_StopsAndCommentsOnce()
    {
        using AutomationHarness harness = await AutomationHarness.CreateAsync();

        // State of the world: the branch carries a commit by someone other than the automation.
        await harness.Head.SeedBranchAsync(
            branch: "update-tools",
            fromBranch: "main",
            relativePath: "hotfix.txt",
            content: "manual patch",
            author: s_foreignAuthor,
            message: "Apply hotfix");
        long pullRequestId =
            harness.PullRequests.SeedOpenPullRequest("update-tools", "main", "Update tools", "Automated update.");

        string headTipBefore = await harness.Head.GetBranchTipAsync("update-tools");

        PullRequestSpec spec = new()
        {
            Key = "update-tools",
            Title = "Update tools",
            Body = "Automated update.",
            TargetBranch = "main",
            // CommentAndStop is the default; applying different content would otherwise update the branch.
            Apply = WriteFile("tools.txt", "v2", "Update tools"),
        };

        PullRequestResult first = await harness.EnsurePullRequestAsync(spec);

        first.Outcome.ShouldBe(PullRequestOutcome.Stopped);
        first.Detail.ShouldNotBeNull();
        first.Detail.ShouldContain(s_foreignAuthor.Name);

        // The foreign work was left untouched and the explanation was posted once.
        (await harness.Head.GetBranchTipAsync("update-tools")).ShouldBe(headTipBefore);
        harness.PullRequests.Comments.Count.ShouldBe(1);
        harness.PullRequests.Comments[0].PullRequestId.ShouldBe(pullRequestId);

        // A scheduled re-run does not post the same comment again.
        PullRequestResult second = await harness.EnsurePullRequestAsync(spec);

        second.Outcome.ShouldBe(PullRequestOutcome.Stopped);
        harness.PullRequests.Comments.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task EnsureBranchContent_NewChanges_FastForwardsBranch()
    {
        using AutomationHarness harness = await AutomationHarness.CreateAsync();
        string tipBefore = await harness.Target.GetBranchTipAsync("main");

        BranchSpec spec = new()
        {
            Branch = "main",
            Apply = WriteFile("notes.txt", "hello", "Add notes"),
        };

        BranchResult result = await harness.EnsureBranchContentAsync(spec);

        result.Outcome.ShouldBe(BranchOutcome.Updated);
        result.Commits.Count.ShouldBe(1);

        string tipAfter = await harness.Target.GetBranchTipAsync("main");
        tipAfter.ShouldNotBe(tipBefore);
        tipAfter.ShouldBe(result.Commits[0].Sha);
        (await harness.Target.GetFileAtRefAsync("main", "notes.txt")).ShouldBe("hello");
    }

    [TestMethod]
    public async Task EnsurePullRequest_DryRun_PushesNothingAndCreatesNoPullRequest()
    {
        GitAutomationOptions options =
            new(Token: string.Empty, Author: AutomationHarness.DefaultAuthor, IsDryRun: true);
        using AutomationHarness harness = await AutomationHarness.CreateAsync(options);

        PullRequestSpec spec = new()
        {
            Key = "update-tools",
            Title = "Update tools",
            Body = "Automated update.",
            TargetBranch = "main",
            Apply = WriteFile("tools.txt", "v1", "Add tools"),
        };

        PullRequestResult result = await harness.EnsurePullRequestAsync(spec);

        result.Outcome.ShouldBe(PullRequestOutcome.DryRun);
        harness.PullRequests.Creates.ShouldBeEmpty();
        (await harness.Head.BranchExistsAsync("update-tools")).ShouldBeFalse();
    }

    private static Func<IGitContext, CancellationToken, Task> WriteFile(
        string relativePath, string content, string commitMessage) =>
        async (context, cancellationToken) =>
        {
            await File.WriteAllTextAsync(
                Path.Combine(context.Directory, relativePath), content, cancellationToken);
            await context.CommitAsync(commitMessage, cancellationToken);
        };
}
