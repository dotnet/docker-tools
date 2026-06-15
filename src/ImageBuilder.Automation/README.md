# Microsoft.DotNet.ImageBuilder.Automation

A small library for automating git commits and pull requests against GitHub
and Azure DevOps repositories. It replaces this repo's previous dependency on
`Microsoft.DotNet.VersionTools.Automation`.

> **Status:** the interface is settled and both hosts (`GitHubRepoHost`,
> `AzdoRepoHost`) are implemented.

## Design: reconciliation, not actions

The library is modeled on Kubernetes/Terraform-style reconciliation. Callers
declare the *desired state* of a pull request or branch plus a stable
identity; the host makes reality match it. There is one interface,
`IRepoHost`, with two verbs:

- `EnsurePullRequestAsync(PullRequestSpec)` — ensure an open pull request in
  this state exists. The spec's `Key` (which doubles as the head branch name)
  is how the pull request is found on later runs: no open PR with the key →
  create it; PR already contains the changes → no-op; PR has different
  content → update it per policy.
- `EnsureBranchAsync(BranchSpec)` — ensure the branch tip contains these
  changes, committing directly (no pull request, fast-forward only).

Idempotency falls out for free: ensuring identical state is always a no-op,
so a scheduled re-run is safe by construction, not by caller discipline. Keys
live in the host (branch names), so the tool itself is stateless.

### Changes are functions, not snapshots

Both specs carry an `ApplyChanges` callback: the library clones the target
branch into a temporary directory, the caller writes files directly into the
working tree, and git determines what changed. Hosting-service APIs are used
only for the things git can't do: pull requests and comments. The callback
may run more than once against fresh checkouts, so it should be a pure
function of the checkout contents.

### Human collaboration is policy, not logic

What to do when someone else has pushed commits to the automation's branch is
genuinely contingent, so it is explicit configuration
(`PullRequestSpec.OnForeignCommits`) rather than a buried guess:

- `CommentAndStop` (default): post a comment explaining how to apply the
  update manually and leave the human's work untouched.
- `Overwrite`: proceed as if the commits were the automation's own.

How re-runs with *new* content update an existing pull request is orthogonal
(`PullRequestSpec.UpdateStrategy`): `Replace` (force-push to exactly the new
changes; CI-visible) or `Append` (add a commit on top; preserves CI on
unchanged content).

### Dry run support is built in

With `GitAutomationOptions.IsDryRun`, all local work (clone, apply, diff
logging) happens, but nothing is pushed and nothing is created or modified.

The git CLI must be available on `PATH`.

## Committing directly to a branch

```csharp
IRepoHost host = new GitHubRepoHost(
    new GitHubRepo("Microsoft", "mcrdocs"),
    new GitAutomationOptions(token, new GitAuthor("bot", "bot@example.com")));

EnsureResult result = await host.EnsureBranchAsync(new BranchSpec
{
    Branch = "main",
    CommitMessage = "Update readmes",
    Apply = async repoRoot =>
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "readme.md"), content),
});
// result.CommitSha is the pushed commit, or null if nothing needed to change.
```

## Ensuring a pull request exists

```csharp
IRepoHost host = new GitHubRepoHost(
    repo: new GitHubRepo("dotnet", "dotnet-docker"),
    options: new GitAutomationOptions(token, author),
    headRepo: new GitHubRepo("dotnet-bot", "dotnet-docker")); // optional fork

EnsureResult result = await host.EnsurePullRequestAsync(new PullRequestSpec
{
    Key = "update-docker-tools-main",
    Title = "Update docker-tools files",
    Body = "Automated update.",
    CommitMessage = "Update docker-tools files",
    TargetBranch = "main",
    UpdateStrategy = PullRequestUpdateStrategy.Append,
    OnForeignCommits = ForeignCommitPolicy.CommentAndStop,
    Apply = async repoRoot => { /* write files under repoRoot */ },
});
```

For Azure DevOps, use `AzdoRepoHost` with an `AzdoRepo` — the rest of the
code is identical.
