// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CsCheck;

namespace Microsoft.DotNet.Automation.Tests;

[TestClass]
public sealed class PullRequestPlannerTests
{
    private const string Workspace = "test-workspace";

    private static readonly AutomationIdentity AutomationIdentity = new("bot", "bot@example.com");

    private static readonly Uri Url = new("https://github.com/dotnet/example/pull/1");

    // A git object SHA-1: a 40-character lowercase hex hash (tree hash, commit SHA, etc.).
    private static readonly Gen<string> GenSha1 = Gen.String[Gen.Char["0123456789abcdef"], 40, 40];

    private static readonly Gen<int> GenPullRequestNumber = Gen.Int[1, 99999];

    private static readonly Gen<PullRequestUpdateStrategy> GenUpdateStrategy =
        Gen.OneOfConst(
            PullRequestUpdateStrategy.Append,
            PullRequestUpdateStrategy.Replace);

    private static readonly Gen<ForeignCommitPolicy> GenForeignCommitPolicy =
        Gen.OneOfConst(
            ForeignCommitPolicy.Proceed,
            ForeignCommitPolicy.Stop);

    private static readonly Gen<PullRequestState> GenPullRequestState =
        from key in Gen.OneOfConst("product-a", "product-b")
        from title in Gen.OneOfConst("Title A", "Title B")
        from body in Gen.OneOfConst("", "Body A", "Body B")
        from targetBranch in Gen.OneOfConst("main", "nightly", "release", "release/1.0")
        from treeHash in GenSha1
        select new PullRequestState(key, title, body, targetBranch, treeHash);

    private static readonly Gen<TargetBranchState> GenTargetBranchState =
        GenSha1.Select(treeHash => new TargetBranchState(treeHash));

    private static readonly Gen<CommitInfo> GenAutomationCommit =
        GenSha1.Select(sha => new CommitInfo(sha, AutomationIdentity.AuthorName, AutomationIdentity.AuthorEmail));

    private static readonly Gen<CommitInfo> GenForeignCommit =
        GenSha1.Select(sha => new CommitInfo(sha, "Person", "person@example.com"));

    private static readonly Gen<CommitInfo> GenRandomCommit = Gen.Frequency((3, GenAutomationCommit), (1, GenForeignCommit));

    private static readonly Gen<CommitInfo[]> GenRandomCommits = GenRandomCommit.Array[1, 3];

    private static Gen<CommitInfo[]> GenCommitsGuaranteedForeign =
        from foreign in GenForeignCommit
        from rest in GenRandomCommit.Array[0, 2]
        from commits in Gen.Shuffle((CommitInfo[])[foreign, .. rest])
        select commits;

    // An arbitrary existing pull request, whose branch may include foreign commits.
    private static readonly Gen<ExistingPullRequest> GenExistingPullRequest =
        from content in GenPullRequestState
        from number in GenPullRequestNumber
        from commits in GenRandomCommits
        select new ExistingPullRequest(content, number, Url, commits);

    // Bundles the planner inputs for a single test case.
    private sealed record PullRequestScenario(
        PullRequestState DesiredState,
        TargetBranchState TargetBranch,
        ExistingPullRequest? ExistingPullRequest,
        PullRequestUpdateStrategy UpdateStrategy,
        ForeignCommitPolicy OnForeignCommits)
    {
        public IEnumerable<IOperation> Plan() =>
            Planner.Plan(
                workspaceDirectory: Workspace,
                identity: AutomationIdentity,
                desiredState: DesiredState,
                targetBranch: TargetBranch,
                existingPullRequest: ExistingPullRequest,
                updateStrategy: UpdateStrategy,
                onForeignCommits: OnForeignCommits);
    };

    // If no PR exists, and there are changes to be made, then a new PR is
    // always created.
    [TestMethod]
    public void ChangesWithNoPR_CreatesNewPR()
    {
        var scenario =
            from desiredState in GenPullRequestState
            from targetBranch in GenTargetBranchState
            from updateStrategy in GenUpdateStrategy
            from onForeignCommits in GenForeignCommitPolicy
                // Collision should be very unlikely, but filter it out anyways
            where desiredState.TreeHash != targetBranch.TreeHash
            select new PullRequestScenario(
                DesiredState: desiredState,
                TargetBranch: targetBranch,
                ExistingPullRequest: null,
                UpdateStrategy: updateStrategy,
                OnForeignCommits: onForeignCommits);

        scenario.Sample(s => s.Plan().OfType<CreatePullRequestOperation>().Count() == 1);
    }

    // If no PR exists, and there are changes to be made, the source branch is
    // always force pushed to reset it to the state of the target branch.
    [TestMethod]
    public void ChangesWithNoPR_ResetsExistingBranch()
    {
        var scenario =
            from desiredState in GenPullRequestState
            from targetBranch in GenTargetBranchState
            from updateStrategy in GenUpdateStrategy
            from onForeignCommits in GenForeignCommitPolicy
            // Collision should be very unlikely, but filter it out anyways
            where desiredState.TreeHash != targetBranch.TreeHash
            select new PullRequestScenario(
                DesiredState: desiredState,
                TargetBranch: targetBranch,
                ExistingPullRequest: null,
                UpdateStrategy: updateStrategy,
                OnForeignCommits: onForeignCommits);

        scenario.Sample(s => s.Plan().OfType<PushCommitsOperation>().Single().ForcePush);
    }

    // For all scenarios where the desired tree is already present in an
    // existing pull request, no actions are taken.
    [TestMethod]
    public void NoChanges_NoOp()
    {
        var scenario =
            from desiredState in GenPullRequestState
            from prNumber in GenPullRequestNumber
            from existingCommits in GenRandomCommits
            from targetBranch in GenTargetBranchState
            from updateStrategy in GenUpdateStrategy
            select new PullRequestScenario(
                desiredState,
                targetBranch,
                new ExistingPullRequest(desiredState, prNumber, Url, existingCommits),
                updateStrategy,
                // Don't block on foreign commits
                ForeignCommitPolicy.Proceed);

        scenario.Sample(s => !s.Plan().Any());
    }

    // For all scenarios where the desired tree already equals the target tree,
    // nothing is pushed.
    [TestMethod]
    public void NoChanges_DoesNotPush()
    {
        var scenario =
            from desiredState in GenPullRequestState
            // 20% of scenarios will have no existing pull request
            from existingPullRequest in Gen.Null(GenExistingPullRequest, 0.2)
            from targetTree in GenSha1
            from updateStrategy in GenUpdateStrategy
            // The base tree is the existing PR's head, or the target branch when none exists.
            // Pin the desired tree to it so there is no content diff to push.
            select new PullRequestScenario(
                desiredState with { TreeHash = existingPullRequest?.Content.TreeHash ?? targetTree },
                new TargetBranchState(targetTree),
                existingPullRequest,
                updateStrategy,
                // Don't block on foreign commits
                ForeignCommitPolicy.Proceed);

        scenario.Sample(s => !s.Plan().OfType<PushCommitsOperation>().Any());
    }

    // If there is already an open PR, never decide to create a second one.
    [TestMethod]
    public void ExistingPR_DoesNotCreateNewPR()
    {
        var scenario =
            from desiredState in GenPullRequestState
            from existingPullRequest in GenExistingPullRequest
            from targetBranch in GenTargetBranchState
            from updateStrategy in GenUpdateStrategy
            from onForeignCommits in GenForeignCommitPolicy
            select new PullRequestScenario(
                desiredState,
                targetBranch,
                existingPullRequest,
                updateStrategy,
                onForeignCommits);

        scenario.Sample(s => !s.Plan().OfType<CreatePullRequestOperation>().Any());
    }

    // For all scenarios where ForeignCommitPolicy.Stop is set, and there is a foreign commit on
    // the PR branch, no action should be taken.
    [TestMethod]
    public void ExistingPR_StopsOnForeignCommits()
    {
        var scenario =
            from desiredState in GenPullRequestState
            from existingState in GenPullRequestState
            from prNumber in GenPullRequestNumber
            from commits in GenCommitsGuaranteedForeign
            from targetBranch in GenTargetBranchState
            from updateStrategy in GenUpdateStrategy
            select new PullRequestScenario(
                desiredState,
                targetBranch,
                new ExistingPullRequest(existingState, prNumber, Url, commits),
                updateStrategy,
                OnForeignCommits: ForeignCommitPolicy.Stop);

        scenario.Sample(s => !s.Plan().Any());
    }
}
