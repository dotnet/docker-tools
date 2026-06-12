// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// Property tests for the <see cref="IRepoHost"/> contract, run against the
/// executable specification <see cref="ModelRepoHost"/>. Each property is
/// quantified over a randomly generated prior world (a seeded repo plus a
/// history of automation runs and human activity) and a randomly generated
/// ensure operation. When the real hosts are implemented, an equivalence
/// property (real host ≅ model on generated scenarios, with git observed via
/// <see cref="LocalRepo"/> and a faked pull request API) transfers all of
/// these properties to them.
/// </summary>
[TestClass]
public class RepoHostPropertyTests
{
    /// <summary>
    /// The defining reconciliation property: ensuring the same desired state
    /// twice changes nothing the second time. The second run never pushes,
    /// never reports Updated/Created, and leaves the world bit-identical.
    /// </summary>
    [TestMethod]
    public void EnsureIsIdempotent() =>
        Gen.Select(AutomationGen.Setup(), AutomationGen.EnsureCommand())
            .Sample(
                (setup, command) =>
                {
                    World world = World.Create(setup);

                    EnsureResult first = world.Execute(command)!;
                    string afterFirst = world.Repo.Snapshot();

                    EnsureResult second = world.Execute(command)!;

                    world.Repo.Snapshot().ShouldBe(afterFirst);
                    second.Outcome.ShouldBeOneOf(EnsureOutcome.Unchanged, EnsureOutcome.Stopped);
                    (second.Outcome == EnsureOutcome.Stopped).ShouldBe(first.Outcome == EnsureOutcome.Stopped);
                }
            );

    /// <summary>
    /// After a successful ensure, the world satisfies the spec: an open pull
    /// request with head branch == Key, synced title/body, and a head tree
    /// equal to the changes applied to the strategy's base (target branch for
    /// Replace/create, current head for Append). Stopped implies the policy
    /// was CommentAndStop, foreign commits really exist, no ref or metadata
    /// moved, and the explanation was delivered.
    /// </summary>
    [TestMethod]
    public void EnsurePullRequestConverges() =>
        Gen.Select(AutomationGen.Setup(), AutomationGen.PrOp())
            .Sample(
                (setup, op) =>
                {
                    World world = World.Create(setup);
                    ModelRepo repo = world.Repo;
                    PullRequestSpec spec = op.Spec;

                    ModelCommit preTargetTip = repo.Branches[AutomationGen.MainBranch];
                    ModelPullRequest? prePr = repo.FindOpenPullRequest(spec.Key);
                    ModelCommit? preHeadTip = repo.Branches.GetValueOrDefault(spec.Key);
                    (string? Title, string? Body) preMetadata = (prePr?.Title, prePr?.Body);
                    string preSnapshot = repo.Snapshot();

                    EnsureResult result = world.Execute(new EnsurePullRequestCommand(op))!;

                    switch (result.Outcome)
                    {
                        case EnsureOutcome.Created:
                        {
                            prePr.ShouldBeNull();
                            ModelPullRequest pr = repo.FindOpenPullRequest(spec.Key).ShouldNotBeNull();
                            pr.Title.ShouldBe(spec.Title);
                            pr.Body.ShouldBe(spec.Body);
                            ModelCommit head = repo.Branches[spec.Key];
                            FsTree.TreesEqual(head.Tree, op.Delta.ApplyTo(preTargetTip.Tree)).ShouldBeTrue();
                            head.Parents.ShouldHaveSingleItem().Sha.ShouldBe(preTargetTip.Sha);
                            result.CommitSha.ShouldBe(head.Sha);
                            result.Url.ShouldBe(pr.Url);
                            break;
                        }

                        case EnsureOutcome.Updated:
                        {
                            prePr.ShouldNotBeNull();
                            prePr.Title.ShouldBe(spec.Title);
                            prePr.Body.ShouldBe(spec.Body);
                            ModelCommit head = repo.Branches[spec.Key];
                            if (result.CommitSha is null)
                            {
                                // Metadata-only update: the branch was not touched and
                                // its content was already as desired.
                                head.ShouldBeSameAs(preHeadTip);
                                (preMetadata.Title != spec.Title || preMetadata.Body != spec.Body).ShouldBeTrue();
                                ImmutableDictionary<string, string> baseTree =
                                    spec.UpdateStrategy == PullRequestUpdateStrategy.Append
                                        ? preHeadTip!.Tree
                                        : preTargetTip.Tree;
                                FsTree.TreesEqual(op.Delta.ApplyTo(baseTree), preHeadTip!.Tree).ShouldBeTrue();
                            }
                            else if (spec.UpdateStrategy == PullRequestUpdateStrategy.Replace)
                            {
                                FsTree.TreesEqual(head.Tree, op.Delta.ApplyTo(preTargetTip.Tree)).ShouldBeTrue();
                                head.Parents.ShouldHaveSingleItem().Sha.ShouldBe(preTargetTip.Sha);
                                result.CommitSha.ShouldBe(head.Sha);
                            }
                            else
                            {
                                // Append: the old head is preserved as the parent.
                                FsTree.TreesEqual(head.Tree, op.Delta.ApplyTo(preHeadTip!.Tree)).ShouldBeTrue();
                                head.Parents.ShouldHaveSingleItem().Sha.ShouldBe(preHeadTip.Sha);
                                result.CommitSha.ShouldBe(head.Sha);
                            }

                            break;
                        }

                        case EnsureOutcome.Unchanged:
                        {
                            repo.Snapshot().ShouldBe(preSnapshot);
                            if (prePr is null)
                            {
                                FsTree
                                    .TreesEqual(op.Delta.ApplyTo(preTargetTip.Tree), preTargetTip.Tree)
                                    .ShouldBeTrue();
                            }
                            else
                            {
                                prePr.Title.ShouldBe(spec.Title);
                                prePr.Body.ShouldBe(spec.Body);
                                ImmutableDictionary<string, string> baseTree =
                                    spec.UpdateStrategy == PullRequestUpdateStrategy.Append
                                        ? preHeadTip!.Tree
                                        : preTargetTip.Tree;
                                FsTree.TreesEqual(op.Delta.ApplyTo(baseTree), preHeadTip!.Tree).ShouldBeTrue();
                            }

                            break;
                        }

                        case EnsureOutcome.Stopped:
                        {
                            spec.OnForeignCommits.ShouldBe(ForeignCommitPolicy.CommentAndStop);
                            repo.Branches[AutomationGen.MainBranch].ShouldBeSameAs(preTargetTip);
                            repo.Branches.GetValueOrDefault(spec.Key).ShouldBeSameAs(preHeadTip);

                            var foreign = repo.ForeignCommits(spec.Key, AutomationGen.MainBranch, AutomationGen.Bot);
                            foreign.ShouldNotBeEmpty();
                            result.Detail.ShouldNotBeNull();
                            foreach (ModelCommit commit in foreign)
                            {
                                result.Detail.ShouldContain(commit.Sha);
                            }

                            if (spec.StopComment is not null)
                            {
                                result.Detail.ShouldContain(spec.StopComment);
                            }

                            if (prePr is not null)
                            {
                                prePr.Title.ShouldBe(preMetadata.Title);
                                prePr.Body.ShouldBe(preMetadata.Body);
                                prePr.Comments.ShouldContain(result.Detail);
                            }

                            break;
                        }

                        default:
                            Assert.Fail($"Unexpected outcome {result.Outcome} from a non-dry-run host.");
                            break;
                    }
                }
            );

    /// <summary>
    /// EnsureBranch leaves the branch tip equal to the changes applied to the
    /// previous tip, fast-forward only (the old tip is the new tip's parent),
    /// or does nothing at all when the changes are already present.
    /// </summary>
    [TestMethod]
    public void EnsureBranchConverges() =>
        Gen.Select(AutomationGen.Setup(), AutomationGen.BranchOp)
            .Sample(
                (setup, op) =>
                {
                    World world = World.Create(setup);
                    ModelRepo repo = world.Repo;
                    ModelCommit preTip = repo.Branches[op.Spec.Branch];
                    string preSnapshot = repo.Snapshot();

                    EnsureResult result = world.Execute(new EnsureBranchCommand(op))!;

                    ModelCommit tip = repo.Branches[op.Spec.Branch];
                    if (result.Outcome == EnsureOutcome.Unchanged)
                    {
                        repo.Snapshot().ShouldBe(preSnapshot);
                        FsTree.TreesEqual(op.Delta.ApplyTo(preTip.Tree), preTip.Tree).ShouldBeTrue();
                        result.CommitSha.ShouldBeNull();
                    }
                    else
                    {
                        result.Outcome.ShouldBe(EnsureOutcome.Updated);
                        FsTree.TreesEqual(tip.Tree, op.Delta.ApplyTo(preTip.Tree)).ShouldBeTrue();
                        tip.Parents.ShouldHaveSingleItem().ShouldBeSameAs(preTip);
                        result.CommitSha.ShouldBe(tip.Sha);
                    }
                }
            );

    /// <summary>
    /// The result is a truthful summary of the state delta: Unchanged means
    /// nothing changed at all, Stopped means no ref moved, a non-null
    /// CommitSha is exactly the tip of a branch that moved, and Created/
    /// Updated faithfully report whether the pull request existed before.
    /// </summary>
    [TestMethod]
    public void EnsureOutcomeIsHonest() =>
        Gen.Select(AutomationGen.Setup(), AutomationGen.EnsureCommand())
            .Sample(
                (setup, command) =>
                {
                    World world = World.Create(setup);
                    ModelRepo repo = world.Repo;
                    string? prKey = (command as EnsurePullRequestCommand)?.Op.Spec.Key;
                    bool preOpenPr = prKey is not null && repo.FindOpenPullRequest(prKey) is not null;
                    Dictionary<string, string> preTips = BranchTips(repo);
                    string preSnapshot = repo.Snapshot();

                    EnsureResult result = world.Execute(command)!;

                    result.Outcome.ShouldNotBe(EnsureOutcome.DryRun);

                    if (result.Outcome == EnsureOutcome.Unchanged)
                    {
                        repo.Snapshot().ShouldBe(preSnapshot);
                    }

                    if (result.Outcome is EnsureOutcome.Unchanged or EnsureOutcome.Stopped)
                    {
                        BranchTips(repo).ShouldBe(preTips);
                        result.CommitSha.ShouldBeNull();
                    }

                    if (result.CommitSha is not null)
                    {
                        result.Outcome.ShouldBeOneOf(EnsureOutcome.Created, EnsureOutcome.Updated);
                        repo.Branches.Values.Select(tip => tip.Sha).ShouldContain(result.CommitSha);
                        preTips.Values.ShouldNotContain(result.CommitSha);
                    }

                    if (result.Outcome == EnsureOutcome.Created)
                    {
                        preOpenPr.ShouldBeFalse();
                        repo.FindOpenPullRequest(prKey!).ShouldNotBeNull();
                        result.CommitSha.ShouldNotBeNull();
                    }

                    if (result.Outcome == EnsureOutcome.Updated && prKey is not null)
                    {
                        preOpenPr.ShouldBeTrue();
                    }
                }
            );

    /// <summary>
    /// A dry run is the identity on remote state, and its outcome correctly
    /// predicts what a real run from the same state would do: DryRun iff the
    /// real run would create or update something, otherwise the same outcome
    /// (and the same explanation) as the real run.
    /// </summary>
    [TestMethod]
    public void DryRunDoesNotMutateAndPredictsRealOutcome() =>
        Gen.Select(AutomationGen.Setup(), AutomationGen.EnsureCommand())
            .Sample(
                (setup, command) =>
                {
                    World world = World.Create(setup);
                    ModelRepo fork = world.Repo.Fork();
                    string preSnapshot = world.Repo.Snapshot();

                    EnsureResult dry = world.Execute(command, World.HostFor(world.Repo, isDryRun: true))!;
                    world.Repo.Snapshot().ShouldBe(preSnapshot);

                    EnsureResult real = world.Execute(command, World.HostFor(fork))!;

                    EnsureOutcome expected = real.Outcome switch
                    {
                        EnsureOutcome.Created or EnsureOutcome.Updated => EnsureOutcome.DryRun,
                        _ => real.Outcome,
                    };
                    dry.Outcome.ShouldBe(expected);

                    if (real.Outcome == EnsureOutcome.Stopped)
                    {
                        dry.Detail.ShouldBe(real.Detail);
                    }
                }
            );

    /// <summary>
    /// The safety invariant that justifies the library: under CommentAndStop,
    /// no human-authored commit ever becomes unreachable, no matter what
    /// sequence of automation runs and human activity occurs. Also: a blocked
    /// update never posts duplicate explanatory comments.
    /// </summary>
    [TestMethod]
    public void HumanWorkIsNeverDestroyed() =>
        AutomationGen
            .Setup(policy: Gen.Const(ForeignCommitPolicy.CommentAndStop), maxCommands: 10)
            .Sample(
                iter: 1000,
                assert: setup =>
                {
                    World world = World.Create(new Setup(setup.MainTree, []));
                    foreach (WorldCommand command in setup.Commands)
                    {
                        world.Execute(command);

                        foreach ((string branch, string sha) in world.HumanCommits)
                        {
                            world
                                .Repo.IsReachableFromAnyBranch(sha)
                                .ShouldBeTrue($"human commit {sha} (pushed to {branch}) was destroyed by '{command}'");
                        }

                        foreach (ModelPullRequest pr in world.Repo.PullRequests)
                        {
                            pr.Comments.Distinct()
                                .Count()
                                .ShouldBe(pr.Comments.Count, $"duplicate comments were posted on {pr.Url}");
                        }
                    }
                }
            );

    /// <summary>
    /// When no open pull request exists for the key, the update strategy is
    /// irrelevant: Replace and Append produce identical worlds (the strategy
    /// only governs how an existing pull request is updated).
    /// </summary>
    [TestMethod]
    public void UpdateStrategiesAgreeWhenNoPullRequestExists() =>
        Gen.Select(AutomationGen.Setup(), AutomationGen.PrOp())
            .Sample(
                (setup, op) =>
                {
                    World world = World.Create(setup);
                    if (world.Repo.FindOpenPullRequest(op.Spec.Key) is not null)
                    {
                        return; // Property only quantifies over worlds without an open PR for the key.
                    }

                    ModelRepo replaceRepo = world.Repo.Fork();
                    ModelRepo appendRepo = world.Repo.Fork();

                    EnsureResult replaceResult = World
                        .HostFor(replaceRepo)
                        .EnsurePullRequestAsync(op.Spec with { UpdateStrategy = PullRequestUpdateStrategy.Replace })
                        .GetAwaiter()
                        .GetResult();
                    EnsureResult appendResult = World
                        .HostFor(appendRepo)
                        .EnsurePullRequestAsync(op.Spec with { UpdateStrategy = PullRequestUpdateStrategy.Append })
                        .GetAwaiter()
                        .GetResult();

                    appendResult.Outcome.ShouldBe(replaceResult.Outcome);
                    appendRepo.Snapshot().ShouldBe(replaceRepo.Snapshot());
                }
            );

    /// <summary>
    /// Ensures with distinct keys are independent: running them in either
    /// order produces the same world.
    /// </summary>
    [TestMethod]
    public void EnsuresWithDistinctKeysCommute() =>
        Gen.Select(
                AutomationGen.Setup(),
                AutomationGen.PrOp(key: Gen.Const(AutomationGen.Keys[0])),
                AutomationGen.PrOp(key: Gen.Const(AutomationGen.Keys[1]))
            )
            .Sample(
                (setup, op1, op2) =>
                {
                    World world = World.Create(setup);
                    ModelRepo repoAB = world.Repo.Fork();
                    ModelRepo repoBA = world.Repo.Fork();

                    ModelRepoHost hostAB = World.HostFor(repoAB);
                    EnsureResult resultA1 = hostAB.EnsurePullRequestAsync(op1.Spec).GetAwaiter().GetResult();
                    EnsureResult resultB1 = hostAB.EnsurePullRequestAsync(op2.Spec).GetAwaiter().GetResult();

                    ModelRepoHost hostBA = World.HostFor(repoBA);
                    EnsureResult resultB2 = hostBA.EnsurePullRequestAsync(op2.Spec).GetAwaiter().GetResult();
                    EnsureResult resultA2 = hostBA.EnsurePullRequestAsync(op1.Spec).GetAwaiter().GetResult();

                    resultA2.Outcome.ShouldBe(resultA1.Outcome);
                    resultB2.Outcome.ShouldBe(resultB1.Outcome);
                    repoBA.Snapshot().ShouldBe(repoAB.Snapshot());
                }
            );

    /// <summary>
    /// The ApplyChanges contract from the other side: the callback is only
    /// ever invoked against a clean checkout whose contents are exactly the
    /// tip of the branch the changes are applied to (the target branch, or
    /// the current head for Append updates).
    /// </summary>
    [TestMethod]
    public void ApplyRunsOnCleanCheckoutOfBranchTip() =>
        Gen.Select(AutomationGen.Setup(), AutomationGen.PrOp())
            .Sample(
                (setup, op) =>
                {
                    World world = World.Create(setup);
                    ModelRepo repo = world.Repo;
                    ImmutableDictionary<string, string> targetTree = repo.Branches[AutomationGen.MainBranch].Tree;
                    ImmutableDictionary<string, string>? headTree = repo.FindOpenPullRequest(op.Spec.Key) is null
                        ? null
                        : repo.Branches[op.Spec.Key].Tree;
                    ImmutableDictionary<string, string> expectedBase =
                        headTree is not null && op.Spec.UpdateStrategy == PullRequestUpdateStrategy.Append
                            ? headTree
                            : targetTree;

                    var observed = new List<ImmutableDictionary<string, string>>();
                    ApplyChanges apply = op.Spec.Apply;
                    PullRequestSpec spec = op.Spec with
                    {
                        Apply = async repoRoot =>
                        {
                            observed.Add(FsTree.Read(repoRoot));
                            await apply(repoRoot);
                        },
                    };

                    world.Execute(new EnsurePullRequestCommand(new PrOp(spec, op.Delta)));

                    observed.ShouldNotBeEmpty();
                    foreach (ImmutableDictionary<string, string> tree in observed)
                    {
                        FsTree.TreesEqual(tree, expectedBase).ShouldBeTrue();
                    }
                }
            );

    private static Dictionary<string, string> BranchTips(ModelRepo repo) =>
        repo.Branches.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Sha);
}
