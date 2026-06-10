# `Microsoft.DotNet.VersionTools.Automation` Migration

This repository no longer depends on `Microsoft.DotNet.VersionTools.Automation`. It has been
replaced by [`Microsoft.DotNet.ImageBuilder.Automation`](../src/ImageBuilder.Automation/README.md),
a small library in this repo that automates git commits and pull requests against GitHub and
Azure DevOps.

## Migration summary

The key difference from VersionTools is that changes are made with the git CLI against a local
clone instead of through GitHub's git database REST APIs. Callers write files into a working
tree; git itself determines what changed. The hosting service's API is only used to create and
update pull requests.

| Old (VersionTools) | New (ImageBuilder.Automation) |
| ------------------ | ----------------------------- |
| `GitHubClient` / `GitHubAuth` | `BranchUpdater`, `GitHubPullRequestUpdater`, `AzdoPullRequestUpdater` |
| `GitObject`, `GitTree`, `GitCommit`, `GitReference` tree/commit plumbing | Write files under the cloned repo root in an `ApplyChanges` callback |
| `GetGitHubFileContentsAsync` + manual content comparison | `git status` on the clone (unchanged files are naturally no-ops) |
| `SearchPullRequestsAsync` + `PostGitHubPullRequestAsync` | `IPullRequestUpdater.CreateOrUpdatePullRequestAsync` with a stable PR `Key` |
| `GitHubProject` / `GitHubBranch` | `GitHubRepo` / `AzdoRepo` records + branch name strings |
| Dry-run wrapper that throws on side effects | `GitAutomationOptions.IsDryRun` (logs the diff, skips push/PR calls) |

Consumers migrated:

1. **`PublishMcrDocsCommand`** (ImageBuilder) — uses `IBranchUpdaterFactory` /
   `IBranchUpdater.PushChangesAsync` to commit readme/tag-metadata updates directly to the
   mcrdocs repo (fast-forward only, commit SHA still exported as `readmeCommitDigest`).
2. **`file-pusher`** (and `yaml-updater`, which invokes it) — uses `GitHubPullRequestUpdater`
   with the `Replace` update strategy: each run force-pushes the PR branch to a fresh state,
   keyed by `{repo}-{branch}{workingBranchSuffix}` (the same head branch name as before, so
   pre-existing open PRs are still found and reused). Re-runs that produce identical content
   push nothing.

Because changes are now pushed with the git CLI, the `file-pusher` and `yaml-updater` runtime
images install `git`, and the ImageBuilder image already had it. The
`System.Formats.Asn1` CVE pin (needed by VersionTools) was removed along with the package.

---

The remainder of this document is the original usage catalog that drove the migration,
kept for historical reference.

## Package References

`Microsoft.DotNet.VersionTools` is referenced by two projects:

| Project | File | Version |
| ------- | ---- | ------- |
| ImageBuilder | `src/ImageBuilder/Microsoft.DotNet.ImageBuilder.csproj` (line 38) | `9.0.0-beta.25255.5` |
| file-pusher | `eng/src/file-pusher/file-pusher.csproj` (line 16) | `9.0.0-beta.25255.5` |

> Note: ImageBuilder's `.csproj` also pins `System.Formats.Asn1` to work around CVE-2024-38095,
> which is implicitly referenced by `Microsoft.DotNet.VersionTools`.

## Key Types Used

From `Microsoft.DotNet.VersionTools.Automation`:

- `GitHubClient` — concrete HTTP client for the GitHub API.
- `GitHubAuth` — authentication info (token, user, email) used to construct a `GitHubClient`.

From `Microsoft.DotNet.VersionTools.Automation.GitHubApi`:

- `IGitHubClient` — interface implemented/wrapped by the code.
- `GitObject` — a file change (path, type, mode, content) for a tree commit.
- `GitHubProject` — repo + owner pair.
- `GitHubBranch` — branch name + project.
- `GitReference` / `GitReferenceObject` — a git ref and its target SHA.
- `GitTree`, `GitCommit` — results of tree/commit creation.
- `GitHubContents` — file contents response.
- `GitHubPullRequest` — pull request model.
- `GitHubCombinedStatus` — combined commit status.
- `PullRequestOptions` — PR capability options.
- `HttpFailureResponseException` — thrown on failed API responses.

## Usage by File

### `src/ImageBuilder/GitHubClientFactory.cs`

Creates and wraps the VersionTools `GitHubClient` for ImageBuilder.

| Method / Member | Type(s) used | What it's used for |
| --------------- | ------------ | ------------------ |
| `GetClientAsync` | `GitHubAuth`, `GitHubClient` | Builds a `GitHubAuth` from a token/username/email and constructs a `GitHubClient`, wrapped in a dry-run-aware `GitHubClientWrapper`. |
| `GitHubClientWrapper` (private class) | `IGitHubClient` and all its members | Implements `IGitHubClient` by delegating to the inner `GitHubClient`, adding retry policies on reads and blocking side-effecting operations during dry runs. Wrapped members include: `Auth`, `AdjustOptionsToCapability`, `CreateGitRemoteUrl`, `GetCommitAsync`, `GetGitHubFileAsync`, `GetGitHubFileContentsAsync`, `GetMyAuthorIdAsync`, `GetReferenceAsync`, `GetStatusAsync`, `PatchReferenceAsync`, `PostCommentAsync`, `PostCommitAsync`, `PostGitHubPullRequestAsync`, `PostReferenceAsync`, `PostTreeAsync`, `PutGitHubFileAsync`, `SearchPullRequestsAsync`, `UpdateGitHubPullRequestAsync`. |

### `src/ImageBuilder/IGitHubClientFactory.cs`

| Method / Member | Type(s) used | What it's used for |
| --------------- | ------------ | ------------------ |
| `GetClientAsync` | `IGitHubClient` | Factory interface returning the VersionTools `IGitHubClient` abstraction. |

### `src/ImageBuilder/GitHelper.cs`

Shared git helpers used by commands.

| Method / Member | Type(s) used | What it's used for |
| --------------- | ------------ | ------------------ |
| `PushChangesAsync` | `IGitHubClient`, `GitHubProject`, `GitHubBranch`, `GitObject`, `GitReference`, `GitTree`, `GitCommit` | Pushes a set of `GitObject` file changes to a branch: reads the current branch ref, posts a tree and commit, then fast-forwards the ref (`PatchReferenceAsync` with `force: false`). Logs would-be changes when dry-run. |
| `GetArchiveUrl`, `GetBlobUrl`, `GetCommitUrl` | `IGitHubBranchRef`, `IGitHubFileRef`, `IGitHubRepoRef` | Build GitHub URLs from ref interfaces (these `IGitHub*Ref` interfaces are ImageBuilder-local, but the file imports the VersionTools namespaces for the `PushChangesAsync` types). |

### `src/ImageBuilder/Commands/PublishMcrDocsCommand.cs`

Publishes README/tag-metadata files to the MCR docs repo.

| Method / Member | Type(s) used | What it's used for |
| --------------- | ------------ | ------------------ |
| `ExecuteAsync` | `IGitHubClient`, `GitObject`, `GitReference` | Gets a client from `IGitHubClientFactory` and calls `GitHelper.PushChangesAsync` (with retry) to mirror readmes/tag metadata; reads the resulting commit SHA from `GitReference`. |
| `FilterUpdatedGitObjectsAsync` | `GitObject`, `IGitHubClient`, `GitHubBranch` | Compares each file's current GitHub content (`GetGitHubFileContentsAsync`) against generated content to filter out unchanged files. |
| `GetGitObject` / `GetUpdatedReadmes` / `GetUpdatedTagsMetadata` | `GitObject` (incl. `GitObject.TypeBlob`, `GitObject.ModeFile`) | Construct `GitObject` instances representing the updated readme and tag-metadata files. |

### `eng/src/file-pusher/FilePusher.cs`

Standalone utility that opens/updates pull requests pushing files to multiple repos.

| Method / Member | Type(s) used | What it's used for |
| --------------- | ------------ | ------------------ |
| `ExecuteGitOperationsWithRetryAsync` | `GitHubAuth`, `GitHubClient` | Builds `GitHubAuth` from token/user/email and constructs a `GitHubClient` used for all git operations, with retry on `HttpRequestException`. |
| `CreatePullRequestAsync` | `GitHubProject`, `GitHubBranch`, `GitObject`, `GitReference`, `GitTree`, `GitCommit`, `GitHubPullRequest` | Creates/updates a branch on a fork (post tree, post commit, patch/post reference) and opens a PR if one doesn't already exist (`SearchPullRequestsAsync`, `PostGitHubPullRequestAsync`). |
| `AddUpdatedFile` / `GetUpdatedFiles` | `GitObject`, `GitHubClient`, `GitHubBranch` | Compares local file contents to current GitHub contents (`GetGitHubFileContentsAsync`) and builds the list of changed `GitObject`s. |
| `BranchExists` | `GitHubClient`, `GitHubProject`, `HttpFailureResponseException` | Checks whether a branch ref exists via `GetReferenceAsync`, treating `HttpFailureResponseException` as "not found". |

## Usage in Tests

### `src/ImageBuilder.Tests/PublishMcrDocsCommandTests.cs`

| Type(s) used | What it's used for |
| ------------ | ------------------ |
| `IGitHubClient`, `GitHubProject`, `GitObject`, `GitHubBranch`, `GitReference`, `GitReferenceObject`, `GitTree`, `GitCommit` | Mocks `IGitHubClient` (`PostTreeAsync`, `GetGitHubFileContentsAsync`, `GetReferenceAsync`, `PostCommitAsync`, `PatchReferenceAsync`) and verifies the `GitObject[]` pushed by `PublishMcrDocsCommand`. |

### `src/ImageBuilder.Tests/GetStaleImagesCommandTests.cs`

| Type(s) used | What it's used for |
| ------------ | ------------------ |
| `GitHubBranch` | Imports `Microsoft.DotNet.VersionTools.Automation` and uses the `GitHubBranch` type in the `IsMatchingBranch` test helper (matching branch name / project owner+repo). |

> Note: `GetStaleImagesCommand` itself no longer uses the VersionTools `GitHubClient`; it uses
> Octokit clients (`ITreesClient`, `IBlobsClient`) via `IOctokitClientFactory`. The only remaining
> VersionTools dependency in its tests is the `GitHubBranch` type in a helper method.

## Summary

The repository depends on `Microsoft.DotNet.VersionTools.Automation` in two areas:

1. **ImageBuilder's GitHub automation** — `GitHubClientFactory` wraps the VersionTools `GitHubClient`
   behind ImageBuilder's `IGitHubClientFactory`/`IGitHubClient`, consumed by `GitHelper.PushChangesAsync`
   and `PublishMcrDocsCommand` to push readme/tag-metadata commits.
2. **The file-pusher utility** — uses VersionTools `GitHubClient`/`GitHubAuth` directly to open and
   update pull requests across repos.

Migration will primarily need replacements for `GitHubClient`/`GitHubAuth` and the `GitHubApi` model
types (`GitObject`, `GitHubProject`, `GitHubBranch`, `GitReference`, `GitTree`, `GitCommit`,
`GitHubPullRequest`, `GitHubContents`, etc.), most likely with Octokit equivalents, which are already
used elsewhere in ImageBuilder (e.g. `GetStaleImagesCommand`).
