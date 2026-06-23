// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.Tests;

/// <summary>
/// The front door of the test harness. Wires real local bare-git remotes (via
/// <see cref="GitTestRepo"/>/<see cref="LocalRemoteRepo"/>) and an in-memory
/// <see cref="FakePullRequestApi"/> into a <see cref="RepoHostEngine"/>.
///
/// A test:
/// <list type="number">
/// <item>creates a harness (the initial state of the world),</item>
/// <item>optionally seeds extra branches/commits (<see cref="Target"/>/<see cref="Head"/>)
/// and pull requests (<see cref="PullRequests"/>),</item>
/// <item>takes an action (<see cref="EnsurePullRequestAsync"/> /
/// <see cref="EnsureBranchContentAsync"/>),</item>
/// <item>asserts on the result and on what was pushed / which pull request
/// operations ran.</item>
/// </list>
/// </summary>
internal sealed class AutomationHarness : IDisposable
{
    /// <summary>The identity the automation commits and pushes as.</summary>
    public static readonly GitAuthor DefaultAuthor = new("automation-bot", "automation@example.test");

    private readonly GitTestRepo _target;
    private readonly GitTestRepo? _fork;
    private readonly RepoHostEngine _engine;

    private AutomationHarness(
        GitTestRepo target, GitTestRepo? fork, FakePullRequestApi pullRequests, RepoHostEngine engine)
    {
        _target = target;
        _fork = fork;
        PullRequests = pullRequests;
        _engine = engine;
    }

    /// <summary>The repository pull requests merge into and branches are pushed to.</summary>
    public GitTestRepo Target => _target;

    /// <summary>
    /// The repository pull request head branches are pushed to. The same as
    /// <see cref="Target"/> unless the harness was created with a fork.
    /// </summary>
    public GitTestRepo Head => _fork ?? _target;

    /// <summary>The in-memory pull request API used to seed and inspect pull requests.</summary>
    public FakePullRequestApi PullRequests { get; }

    /// <summary>Creates a harness whose target repo has a single commit on <paramref name="targetBranch"/>.</summary>
    /// <param name="options">Automation settings; defaults to <see cref="DefaultAuthor"/> with an empty token.</param>
    /// <param name="targetBranch">The branch the target repo is initialized with.</param>
    /// <param name="useFork">When true, head branches are pushed to a separate fork remote.</param>
    public static async Task<AutomationHarness> CreateAsync(
        GitAutomationOptions? options = null,
        string targetBranch = "main",
        bool useFork = false)
    {
        options ??= new GitAutomationOptions(Token: string.Empty, Author: DefaultAuthor);

        GitTestRepo target = await GitTestRepo.InitAsync(options.Author, targetBranch);
        GitTestRepo? fork = useFork ? await GitTestRepo.InitAsync(options.Author, targetBranch) : null;

        FakePullRequestApi pullRequests = new();
        LocalRemoteRepo targetRepo = new(target.Url);
        LocalRemoteRepo headRepo = new((fork ?? target).Url);

        RepoHostEngine engine = new(targetRepo, headRepo, pullRequests, options);

        return new AutomationHarness(target, fork, pullRequests, engine);
    }

    public Task<PullRequestResult> EnsurePullRequestAsync(
        PullRequestSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsurePullRequestAsync(spec, cancellationToken);

    public Task<BranchResult> EnsureBranchContentAsync(
        BranchSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsureBranchContentAsync(spec, cancellationToken);

    public void Dispose()
    {
        _target.Dispose();
        _fork?.Dispose();
    }
}
