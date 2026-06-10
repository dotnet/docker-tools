# Git automation interface design

John Ousterhout would say: the interface should be much simpler than the mess it hides (merge conflicts, dedup, host APIs), and it should be *declarative*, because "create a PR" is the wrong abstraction — what you actually want is "ensure a PR in this state exists." That single move solves most of your list.

## Core idea: reconciliation, not actions

Model it like Kubernetes/Terraform. The caller declares desired state plus an identity; the module reconciles reality toward it.

```python
class ChangeSet:
    key: str              # stable identity, e.g. "dep-update/lockfile"
    title: str
    body: str
    base: str
    files: FileEdits      # or a function repo -> edits
    fingerprint: str | None = None   # optional; defaults to hash of content

result = host.ensure_pull_request(changeset)
```

Semantics of `ensure_pull_request`:

- No open PR with this `key` (stored as a branch-name convention or hidden marker in the body) → create branch, apply edits, open PR.
- Open PR exists, fingerprint matches → **no-op**. This is your dependency-update case: the version lives in the fingerprint (caller-supplied or content-derived), not in the interface. The module knows nothing about versions; it only knows "same key, same fingerprint, do nothing."
- Open PR exists, fingerprint differs → update it, per a policy (below).

## The human-collaboration problem as policy, not logic

The "someone pushed to my branch" question is genuinely contingent, so make it explicit config rather than burying a guess:

```python
on_foreign_commits: "abandon" | "rebase_mine" | "stack_on_top" | "comment_and_stop"
```

Detection is mechanical (commits not authored by the bot since its last push); the *decision* is the caller's. Default to the safe one (`comment_and_stop`). This is Ousterhout's "define errors out of existence" applied socially — instead of force-pushing and apologizing, the common case never destroys anything.

## Issues: same shape

```python
host.ensure_issue(key="infra/disk-full/db-host-3", title=..., body=...)
```

- Key matching: search for open issues carrying the key (marker in body or a label).
- Open issue with key exists → no-op, or append a comment if fingerprint changed ("still happening, count: 14").
- Closed issue with same key → policy again: `reopen | new_issue | stay_silent` (the "we fixed it, stop telling us" vs. "it regressed" distinction).

## Why this is a deep module

The interface is essentially two verbs (`ensure_pull_request`, `ensure_issue`) plus declarative policies. Hidden underneath: branch naming, force-push safety, key encoding/discovery, GitHub vs GitLab vs Gerrit API differences, rate limits, retries, search pagination. Callers never see git plumbing or REST.

A few corollaries:

- **Idempotency falls out for free** — `ensure` of identical state is always a no-op, so the daily cron is safe by construction, not by caller discipline.
- **Keys are the only state**, and they live *in the host* (branch names, body markers), so the tool itself is stateless — no local DB to drift out of sync.
- **Fingerprint is the escape hatch** you were reaching for with "key + config": key = identity ("which PR is this"), fingerprint = version ("has its content changed"). Two concepts, kept orthogonal.

One design tension worth deciding early: does `files` take a snapshot of edits, or a function executed against a fresh checkout? The function form handles "rebase mine" gracefully (re-run the edit on the new base) and keeps callers from doing their own git work — I'd pick that, at the cost of a slightly less obvious interface.

## Rough draft of C# interface

```cs
namespace RepoReconciler;

// ─── The one deep module, instantiated three times ───────────────────────────
// Everything is (key, desired state, change policy) → reconcile against host.

public interface IRepoHost
{
    Task<EnsureResult> EnsurePullRequestAsync(ChangeSet change, CancellationToken ct = default);
    Task<EnsureResult> EnsureIssueAsync(IssueSpec issue, CancellationToken ct = default);
    Task<EnsureResult> EnsureCommentAsync(CommentSpec comment, CancellationToken ct = default);
}

// ─── Pull requests ────────────────────────────────────────────────────────────

public sealed record ChangeSet
{
    public required ChangeKey Key { get; init; }          // stable identity
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string BaseBranch { get; init; }
    public required Func<IWorkspace, Task> Build { get; init; }  // pure fn of checkout
    public string? Fingerprint { get; init; }             // null → tree hash of result
    public ForeignCommitPolicy OnForeignCommits { get; init; } = ForeignCommitPolicy.CommentAndStop;
}

public enum ForeignCommitPolicy { Abandon, RebaseMine, StackOnTop, CommentAndStop }

// Narrow workspace handle: no push, no branches, no remotes.
public interface IWorkspace
{
    Task<string> ReadAsync(string path);
    Task WriteAsync(string path, string content);
    Task<CommandResult> RunAsync(string command, params string[] args);
    Task CommitAsync(string message);
}

// ─── Issues ───────────────────────────────────────────────────────────────────

public sealed record IssueSpec
{
    public required ChangeKey Key { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? Fingerprint { get; init; }
    public ClosedIssuePolicy OnClosed { get; init; } = ClosedIssuePolicy.StaySilent;
    public bool CommentOnRecurrence { get; init; } = true; // "still happening" append
}

public enum ClosedIssuePolicy { Reopen, NewIssue, StaySilent }

// ─── Comments ─────────────────────────────────────────────────────────────────

public sealed record CommentSpec
{
    public required CommentTarget Target { get; init; }
    public required ChangeKey Key { get; init; }
    public string? Body { get; init; }                    // null → resolve/clear
    public CommentChangePolicy OnChange { get; init; } = CommentChangePolicy.Edit;
    public ResolvePolicy WhenCleared { get; init; } = ResolvePolicy.MarkResolved;
}

public enum CommentChangePolicy { Edit, Append, Repost }
public enum ResolvePolicy { MarkResolved, Delete, Keep }

// Target by *our* key when possible — stable across close/reopen, no host IDs leak.
public abstract record CommentTarget
{
    public sealed record ByKey(ChangeKey Key) : CommentTarget;
    public sealed record ByNumber(int Number) : CommentTarget; // escape hatch
}

// ─── Shared vocabulary ────────────────────────────────────────────────────────

public readonly record struct ChangeKey(string Value)
{
    public static implicit operator ChangeKey(string value) => new(value);
}

public sealed record EnsureResult
{
    public required EnsureOutcome Outcome { get; init; }
    public string? Url { get; init; }
    public string? Detail { get; init; }                  // e.g. why we stopped
}

public enum EnsureOutcome { Created, Updated, Unchanged, Stopped, Resolved }

public sealed record CommandResult(int ExitCode, string Stdout, string Stderr);

// ─── Host adapters (the hidden complexity lives behind these) ────────────────

public static class RepoHosts
{
    public static IRepoHost GitHub(string repo, GitHubOptions? options = null) => throw null!;
    public static IRepoHost Azdo(string repo, AzdoOptions? options = null) => throw null!;
}

public sealed record GitHubOptions { /* auth, key-marker style, rate limits… */ }
public sealed record AzdoOptions;
```
