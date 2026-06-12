// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>An ensure-pull-request operation plus its pure denotation.</summary>
internal sealed record PrOp(PullRequestSpec Spec, TreeDelta Delta);

/// <summary>An ensure-branch operation plus its pure denotation.</summary>
internal sealed record BranchOp(BranchSpec Spec, TreeDelta Delta);

internal abstract record WorldCommand;

internal sealed record EnsurePullRequestCommand(PrOp Op) : WorldCommand;

internal sealed record EnsureBranchCommand(BranchOp Op) : WorldCommand;

internal sealed record HumanPushCommand(string Branch, TreeDelta Delta) : WorldCommand;

internal sealed record ClosePullRequestCommand(string Key) : WorldCommand;

internal sealed record MergePullRequestCommand(string Key) : WorldCommand;

/// <summary>A randomly generated initial world: a seeded default branch plus a history of commands.</summary>
internal sealed record Setup(ImmutableDictionary<string, string> MainTree, List<WorldCommand> Commands)
{
    public override string ToString() =>
        $"Setup(main: [{string.Join(", ", MainTree.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => $"{kvp.Key}={kvp.Value.Replace("\n", "\\n")}"))}], "
        + $"commands: [{string.Join("; ", Commands)}])";
}

/// <summary>CsCheck generators for the automation library's domain.</summary>
internal static class AutomationGen
{
    public const string MainBranch = "main";

    public static readonly GitAuthor Bot = new("dotnet-docker-bot", "bot@example.com");
    public static readonly GitAuthor Human = new("Jane Developer", "jane@example.com");
    public static readonly GitAuthor Seeder = new("Repo Seeder", "seeder@example.com");

    public static readonly string[] Keys = ["auto/update-a", "auto/update-b"];

    // A small universe of paths and contents so that collisions (no-op deltas,
    // deletes that hit real files, identical re-writes) actually occur.
    private static readonly string[] s_paths =
    [
        "a.txt",
        "b.txt",
        "notes.md",
        "docs/readme.md",
        "src/app.cs",
        "src/util.cs",
    ];

    private static readonly Gen<string> s_content = Gen.OneOfConst("", "alpha", "bravo", "charlie", "delta\n", "0");

    public static Gen<ImmutableDictionary<string, string>> Tree =>
        from mask in Gen.Int[0, (1 << s_paths.Length) - 1]
        from contents in s_content.Array[s_paths.Length, s_paths.Length]
        select BuildTree(mask, contents);

    public static Gen<TreeDelta> Delta =>
        from writeMask in Gen.Int[0, (1 << s_paths.Length) - 1]
        from contents in s_content.Array[s_paths.Length, s_paths.Length]
        from deleteMask in Gen.Int[0, (1 << s_paths.Length) - 1]
        select BuildDelta(writeMask, contents, deleteMask);

    public static Gen<PullRequestUpdateStrategy> Strategy =>
        Gen.OneOfConst(PullRequestUpdateStrategy.Replace, PullRequestUpdateStrategy.Append);

    public static Gen<ForeignCommitPolicy> AnyPolicy =>
        Gen.OneOfConst(ForeignCommitPolicy.CommentAndStop, ForeignCommitPolicy.Overwrite);

    public static Gen<PrOp> PrOp(Gen<string>? key = null, Gen<ForeignCommitPolicy>? policy = null) =>
        from k in key ?? Gen.OneOfConst(Keys)
        from title in Gen.OneOfConst("Update files", "Automated update", "Sync content")
        from body in Gen.OneOfConst("Automated update.", "Routine sync of generated files.")
        from message in Gen.OneOfConst("Apply update", "Refresh files")
        from delta in Delta
        from strategy in Strategy
        from pol in policy ?? AnyPolicy
        from stopComment in Gen.Bool.Select(string? (b) => b ? "Run `update.sh` to apply this update manually." : null)
        select new PrOp(
            new PullRequestSpec
            {
                Key = k,
                Title = title,
                Body = body,
                CommitMessage = message,
                TargetBranch = MainBranch,
                Apply = delta.ToApplyChanges(),
                UpdateStrategy = strategy,
                OnForeignCommits = pol,
                StopComment = stopComment,
            },
            delta
        );

    public static Gen<BranchOp> BranchOp =>
        from message in Gen.OneOfConst("Update readmes", "Refresh content")
        from delta in Delta
        select new BranchOp(
            new BranchSpec
            {
                Branch = MainBranch,
                CommitMessage = message,
                Apply = delta.ToApplyChanges(),
            },
            delta
        );

    /// <summary>An ensure operation (pull request or branch) as a command.</summary>
    public static Gen<WorldCommand> EnsureCommand(Gen<ForeignCommitPolicy>? policy = null) =>
        Gen.OneOf(
            PrOp(policy: policy).Select(WorldCommand (op) => new EnsurePullRequestCommand(op)),
            BranchOp.Select(WorldCommand (op) => new EnsureBranchCommand(op))
        );

    // Weighted so that the interesting collisions — humans pushing to the
    // automation's head branches between ensure runs — are common rather
    // than coincidental.
    public static Gen<WorldCommand> Command(Gen<ForeignCommitPolicy>? policy = null) =>
        Gen.Frequency(
            (4, EnsureCommand(policy)),
            (
                3,
                from branch in Gen.Frequency((1, Gen.Const(MainBranch)), (2, Gen.OneOfConst(Keys)))
                from delta in Delta
                select (WorldCommand)new HumanPushCommand(branch, delta)
            ),
            (1, Gen.OneOfConst(Keys).Select(WorldCommand (k) => new ClosePullRequestCommand(k))),
            (1, Gen.OneOfConst(Keys).Select(WorldCommand (k) => new MergePullRequestCommand(k)))
        );

    public static Gen<Setup> Setup(Gen<ForeignCommitPolicy>? policy = null, int maxCommands = 6) =>
        from tree in Tree
        from commands in Command(policy).List[0, maxCommands]
        select new Setup(tree, commands);

    private static ImmutableDictionary<string, string> BuildTree(int mask, string[] contents)
    {
        ImmutableDictionary<string, string>.Builder builder = ImmutableDictionary.CreateBuilder<string, string>();
        for (int i = 0; i < s_paths.Length; i++)
        {
            if ((mask >> i & 1) != 0)
            {
                builder[s_paths[i]] = contents[i];
            }
        }

        return builder.ToImmutable();
    }

    private static TreeDelta BuildDelta(int writeMask, string[] contents, int deleteMask)
    {
        ImmutableSortedDictionary<string, string>.Builder writes = ImmutableSortedDictionary.CreateBuilder<
            string,
            string
        >(StringComparer.Ordinal);
        ImmutableSortedSet<string>.Builder deletes = ImmutableSortedSet.CreateBuilder<string>(StringComparer.Ordinal);
        for (int i = 0; i < s_paths.Length; i++)
        {
            if ((writeMask >> i & 1) != 0)
            {
                writes[s_paths[i]] = contents[i];
            }
            else if ((deleteMask >> i & 1) != 0)
            {
                deletes.Add(s_paths[i]);
            }
        }

        return new TreeDelta(writes.ToImmutable(), deletes.ToImmutable());
    }
}

/// <summary>
/// Executes <see cref="WorldCommand"/>s against a <see cref="ModelRepo"/>:
/// ensure commands go through a <see cref="ModelRepoHost"/> acting as the
/// automation; the rest simulate humans pushing commits and closing or
/// merging pull requests. Tracks every human-authored commit so properties
/// can assert none is ever destroyed.
/// </summary>
internal sealed class World
{
    public ModelRepo Repo { get; }

    public ModelRepoHost Host { get; }

    public List<(string Branch, string Sha)> HumanCommits { get; } = [];

    private World(ModelRepo repo)
    {
        Repo = repo;
        Host = HostFor(repo);
    }

    public static World Create(Setup setup)
    {
        var world = new World(ModelRepo.Create(AutomationGen.MainBranch, AutomationGen.Seeder, setup.MainTree));
        foreach (WorldCommand command in setup.Commands)
        {
            world.Execute(command);
        }

        return world;
    }

    public static ModelRepoHost HostFor(ModelRepo repo, bool isDryRun = false) =>
        new(repo, new GitAutomationOptions("test-token", AutomationGen.Bot, isDryRun));

    public EnsureResult? Execute(WorldCommand command, ModelRepoHost? host = null)
    {
        host ??= Host;
        switch (command)
        {
            case EnsurePullRequestCommand c:
                return host.EnsurePullRequestAsync(c.Op.Spec).GetAwaiter().GetResult();

            case EnsureBranchCommand c:
                return host.EnsureBranchAsync(c.Op.Spec).GetAwaiter().GetResult();

            case HumanPushCommand c:
                if (Repo.Branches.TryGetValue(c.Branch, out ModelCommit? tip))
                {
                    ImmutableDictionary<string, string> newTree = c.Delta.ApplyTo(tip.Tree);
                    if (!FsTree.TreesEqual(newTree, tip.Tree))
                    {
                        ModelCommit commit = Repo.Push(c.Branch, AutomationGen.Human, "Manual fix", newTree);
                        HumanCommits.Add((c.Branch, commit.Sha));
                    }
                }

                return null;

            case ClosePullRequestCommand c:
                if (Repo.FindOpenPullRequest(c.Key) is ModelPullRequest toClose)
                {
                    toClose.State = ModelPullRequestState.Closed;
                }

                return null;

            case MergePullRequestCommand c:
                if (Repo.FindOpenPullRequest(c.Key) is ModelPullRequest toMerge)
                {
                    Repo.MergePullRequest(toMerge, AutomationGen.Human);
                }

                return null;

            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }
}
