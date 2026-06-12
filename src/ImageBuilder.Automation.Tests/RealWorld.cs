// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// The real-world counterpart of <see cref="World"/>: executes the same
/// <see cref="WorldCommand"/>s against an actual git repository (a bare
/// "origin" observed by the engine through <see cref="LocalRepo"/>) and a
/// <see cref="FakePullRequestApi"/>. Ensure commands run through the real
/// <see cref="RepoHostEngine"/>; human pushes, closes, and merges are
/// replicated with git plumbing to mirror <see cref="ModelRepo"/> exactly
/// (a merge reuses the head commit's tree, like the model does).
/// <see cref="ToModelRepo"/> reads the resulting state back into a
/// <see cref="ModelRepo"/> so worlds can be compared via snapshots.
/// </summary>
internal sealed class RealWorld : IDisposable
{
    private readonly string _root;
    private readonly string _originGitDir;
    private readonly string _workDir;
    private readonly GitRunner _git = new(NullLogger.Instance, secret: null);
    private readonly Dictionary<string, ImmutableDictionary<string, string>> _treeCache = [];
    private readonly Dictionary<string, string> _blobCache = [];

    public FakePullRequestApi Api { get; } = new();

    public LocalRepo Origin { get; }

    public List<(string Branch, string Sha)> HumanCommits { get; } = [];

    private RealWorld(string root)
    {
        _root = root;
        _originGitDir = Path.Combine(root, "origin.git");
        _workDir = Path.Combine(root, "work");
        Origin = new LocalRepo(_originGitDir);
    }

    public static RealWorld Create(ImmutableDictionary<string, string> mainTree)
    {
        string root = Path.Combine(Path.GetTempPath(), $"automation-equivalence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var world = new RealWorld(root);
        world.Git(root, "init", "--bare", "--initial-branch", AutomationGen.MainBranch, world._originGitDir);
        world.Git(root, "init", "--initial-branch", AutomationGen.MainBranch, world._workDir);
        world.GitWork("remote", "add", "origin", world._originGitDir);

        FsTree.Write(world._workDir, mainTree);
        world.GitWork("add", "--all");
        world.CommitWork(AutomationGen.Seeder, "Seed repository");
        world.GitWork("push", "origin", AutomationGen.MainBranch);

        return world;
    }

    public RepoHostEngine HostFor(bool isDryRun = false) =>
        new(Origin, Origin, Api, new GitAutomationOptions("", AutomationGen.Bot, isDryRun), NullLogger.Instance);

    public EnsureResult? Execute(WorldCommand command, RepoHostEngine? host = null)
    {
        host ??= HostFor();
        switch (command)
        {
            case EnsurePullRequestCommand c:
                return host.EnsurePullRequestAsync(c.Op.Spec).GetAwaiter().GetResult();

            case EnsureBranchCommand c:
                return host.EnsureBranchAsync(c.Op.Spec).GetAwaiter().GetResult();

            case HumanPushCommand c:
                HumanPush(c.Branch, c.Delta);
                return null;

            case ClosePullRequestCommand c:
                if (Api.FindOpenByHead(c.Key) is FakePullRequest toClose)
                {
                    toClose.State = ModelPullRequestState.Closed;
                }

                return null;

            case MergePullRequestCommand c:
                Merge(c.Key);
                return null;

            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    /// <summary>
    /// Reads the origin repository and the fake API back into a
    /// <see cref="ModelRepo"/>. Commit SHAs are not preserved (the model
    /// generates its own), but <see cref="ModelRepo.Snapshot"/> is SHA-free,
    /// so snapshots remain comparable.
    /// </summary>
    public ModelRepo ToModelRepo()
    {
        // sha -> (parent shas, author name, author email, tree sha, subject)
        var rawCommits = new Dictionary<string, string[]>();
        string log = GitOrigin("log", "--all", "--format=%H%x1f%P%x1f%an%x1f%ae%x1f%T%x1f%s");
        foreach (string line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split('\x1f');
            rawCommits[fields[0]] = fields;
        }

        var built = new Dictionary<string, ModelCommit>();
        ModelCommit Build(string sha)
        {
            if (built.TryGetValue(sha, out ModelCommit? existing))
            {
                return existing;
            }

            string[] fields = rawCommits[sha];
            string[] parents = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commit = new ModelCommit
            {
                Author = new GitAuthor(fields[2], fields[3]),
                Message = fields[5],
                Tree = ReadTree(fields[4]),
                Parents = [.. parents.Select(Build)],
            };
            built[sha] = commit;
            return commit;
        }

        ModelRepo repo = ModelRepo.Create(
            AutomationGen.MainBranch, AutomationGen.Seeder, ImmutableDictionary<string, string>.Empty);
        repo.Branches.Clear();

        string refs = GitOrigin("for-each-ref", "refs/heads", "--format=%(refname:short)%00%(objectname)");
        foreach (string line in refs.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split('\0');
            repo.Branches[fields[0]] = Build(fields[1]);
        }

        foreach (FakePullRequest pr in Api.PullRequests)
        {
            repo.PullRequests.Add(new ModelPullRequest
            {
                HeadBranch = pr.HeadBranch,
                TargetBranch = pr.TargetBranch,
                Url = pr.Url,
                Title = pr.Title,
                Body = pr.Body,
                State = pr.State,
                Comments = [.. pr.Comments],
            });
        }

        return repo;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void HumanPush(string branch, TreeDelta delta)
    {
        if (!_git.TryRunAsync(_originGitDir, "rev-parse", "--verify", $"refs/heads/{branch}")
                .GetAwaiter().GetResult())
        {
            return;
        }

        GitWork("fetch", "origin", branch);
        GitWork("checkout", "-B", branch, "FETCH_HEAD");
        delta.ToApplyChanges()(_workDir).GetAwaiter().GetResult();
        GitWork("add", "--all");

        if (string.IsNullOrWhiteSpace(GitWork("status", "--porcelain")))
        {
            return;
        }

        CommitWork(AutomationGen.Human, "Manual fix");
        string sha = GitWork("rev-parse", "HEAD");
        GitWork("push", "origin", $"HEAD:refs/heads/{branch}");
        HumanCommits.Add((branch, sha));
    }

    private void Merge(string key)
    {
        if (Api.FindOpenByHead(key) is not FakePullRequest pr)
        {
            return;
        }

        // Mirror ModelRepo.MergePullRequest: a merge commit whose tree is the
        // head tip's tree, with [target, head] as parents.
        string targetSha = GitOrigin("rev-parse", $"refs/heads/{pr.TargetBranch}");
        string headSha = GitOrigin("rev-parse", $"refs/heads/{pr.HeadBranch}");
        string headTree = GitOrigin("rev-parse", $"{headSha}^{{tree}}");
        string mergeSha = GitOrigin(
            "-c", $"user.name={AutomationGen.Human.Name}",
            "-c", $"user.email={AutomationGen.Human.Email}",
            "commit-tree", headTree, "-p", targetSha, "-p", headSha, "-m", $"Merge {pr.HeadBranch}");
        GitOrigin("update-ref", $"refs/heads/{pr.TargetBranch}", mergeSha);
        pr.State = ModelPullRequestState.Merged;
    }

    private ImmutableDictionary<string, string> ReadTree(string treeSha)
    {
        if (_treeCache.TryGetValue(treeSha, out ImmutableDictionary<string, string>? cached))
        {
            return cached;
        }

        ImmutableDictionary<string, string>.Builder tree = ImmutableDictionary.CreateBuilder<string, string>();
        string lsTree = GitOrigin("ls-tree", "-r", "--format=%(objectname)%x00%(path)", treeSha);
        foreach (string line in lsTree.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split('\0');
            tree[fields[1]] = ReadBlob(fields[0]);
        }

        ImmutableDictionary<string, string> result = tree.ToImmutable();
        _treeCache[treeSha] = result;
        return result;
    }

    private string ReadBlob(string blobSha)
    {
        if (!_blobCache.TryGetValue(blobSha, out string? content))
        {
            content = _git.RunRawAsync(_originGitDir, "cat-file", "blob", blobSha).GetAwaiter().GetResult();
            _blobCache[blobSha] = content;
        }

        return content;
    }

    private void CommitWork(GitAuthor author, string message) =>
        GitWork(
            "-c", $"user.name={author.Name}",
            "-c", $"user.email={author.Email}",
            "commit", "--allow-empty", "-m", message);

    private string GitOrigin(params string[] args) => Git(_originGitDir, args);

    private string GitWork(params string[] args) => Git(_workDir, args);

    private string Git(string workingDirectory, params string[] args) =>
        _git.RunAsync(workingDirectory, args).GetAwaiter().GetResult();
}
