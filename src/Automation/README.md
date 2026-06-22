# Microsoft.DotNet.Automation

A library for automating git commits and pull requests against GitHub
repositories. Requires the git CLI on `PATH`.

Re-running any operation with the same desired state is a no-op, so scheduled
runs are safe.

## Authentication

You pass a single token via `GitAutomationOptions.Token`. A GitHub App
installation token or a personal access token both work; it needs write access
to push and to open pull requests.

The token may be empty for read-only/anonymous access (e.g. a dry run against a
public repo).

## Open or update a pull request

`EnsurePullRequestAsync` opens a pull request, or updates the open one it finds
by `Key` (which is also the head branch name).

```csharp
IRepoHost host = new GitHubRepoHost(
    repo: new GitHubRepo("dotnet", "dotnet-docker"),
    options: new GitAutomationOptions(token, author),
    headRepo: new GitHubRepo("dotnet-bot", "dotnet-docker")); // optional fork

PullRequestSpec spec = new()
{
    Key = "update-docker-tools-main", // also the head branch name
    Title = "Update docker-tools files",
    Body = "Automated update.",
    TargetBranch = "main",
    UpdateStrategy = PullRequestUpdateStrategy.Append,
    OnForeignCommits = ForeignCommitPolicy.CommentAndStop,
    Apply = async (context, cancellationToken) =>
    {
        /* write files under context.Directory */
        await context.CommitAsync("Update docker-tools files", cancellationToken);
    },
};

AutomationResult result = await host.EnsurePullRequestAsync(spec);
```

## Commit directly to a branch

`EnsureBranchContentAsync` commits changes straight to a branch using a
fast-forward push. It does not open a pull request.

```csharp
BranchSpec spec = new()
{
    Branch = "main",
    Apply = async (context, cancellationToken) =>
    {
        await File.WriteAllTextAsync(Path.Combine(context.Directory, "readme.md"), content, cancellationToken);
        await context.CommitAsync("Update readmes", cancellationToken);
    },
};

AutomationResult result = await host.EnsureBranchContentAsync(spec);
// result.CommitShas contains the pushed commits, or is empty if nothing needed to change.
```

## Multiple commits

Call `IGitContext.CommitAsync` each time the current working tree should become
its own commit. Empty commits are skipped automatically.

```csharp
PullRequestSpec spec = new()
{
    Key = "update-docker-tools-main",
    Title = "Update docker-tools files",
    Body = "Automated update.",
    TargetBranch = "main",
    Apply = async (context, cancellationToken) =>
    {
        await UpdateScriptsAsync(context.Directory, cancellationToken);
        await context.CommitAsync("Update shared docker-tools scripts", cancellationToken);

        await UpdateTemplatesAsync(context.Directory, cancellationToken);
        await context.CommitAsync("Update pipeline templates", cancellationToken);
    },
};
```

If no commits are produced, `EnsurePullRequestAsync` does not open a pull
request.

## Options

`PullRequestSpec.UpdateStrategy` controls how a re-run updates an existing pull
request:

| `PullRequestUpdateStrategy` | Behavior |
| --- | --- |
| `Append` (default) | Add the new changes as a commit on top of the branch's existing history. |
| `Replace` | Force-push so the branch is exactly the new changes on top of the target branch. |

`PullRequestSpec.OnForeignCommits` controls what to do when the branch contains
commits authored by someone other than the automation (e.g. another actor
pushed a fix to the bot's branch):

| `ForeignCommitPolicy` | Behavior |
| --- | --- |
| `CommentAndStop` (default) | Leave the branch untouched and post a comment explaining why the update was skipped. |
| `Proceed` | Apply `UpdateStrategy` anyway. With `Append` the foreign commits are kept; with `Replace` they are discarded by the force-push. |

In every case, if the desired content already matches the branch, nothing is
pushed.
