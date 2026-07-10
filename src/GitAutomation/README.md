# Microsoft.DotNet.GitAutomation

`Microsoft.DotNet.GitAutomation` is a library for declaratively managing
pull requests and (eventually) issues.

This pattern is most useful for automation that repeatedly opens or updates
similar pull requests or issues, like dependency or version updates.

## Features

| Feature | GitHub | Azure DevOps |
| ------- | ------ | ------------ |
| Pull requests | ✅ | - |
| Issues | - | - |
| Groups of issues | - | - |

The feature matrix will be filled out incrementally (as needed) in order to
accomplish https://github.com/dotnet/docker-tools/issues/1658.

## Usage

### Open a pull request

```csharp
using Microsoft.DotNet.GitAutomation;
using Microsoft.DotNet.GitAutomation.GitHub;

// Instantiate the pull request manager.
var pullRequestManager = new PullRequestManager(
    token: Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "",
    identity: new AutomationIdentity("bot", "bot@example.com"),
    // Microsoft.Extensions.Logging support:
    // loggerFactory: ...
);

// Declare the pull request that you want to create.
var pullRequest = new PullRequestDefinition(
    // `Key` is the sole method used to identify pull requests managed by this automation.
    // There will never be two pull requests open against the same repo with the same key.
    // To open two simultaneous pull requests against the same repo, use different keys.
    // All other properties of a pull request definition are free to change between runs.
    Key: "version-updates/update-generated-files",
    Title: "Update dependencies",
    Body: "...",
    TargetBranch: "main",
    ApplyChanges: async (git, ct) =>
    {
        string generatedFile = Path.Combine(git.WorkspaceDirectory, "generated.txt");
        await File.WriteAllTextAsync(generatedFile, "...", ct);
        await git.CommitAsync("Update generated.txt", ct);
    }
);

// The manager either creates a new pull request or updates the existing pull
// request if one is already open.
PullRequestResult result = await pullRequestManager.CreateOrUpdateAsync(
    definition: pullRequest,
    upstream: new GitHubRepo("dotnet", "example"),

    // To submit the PR from a fork:
    // fork: new GitHubRepo("bot-account", "example"),

    // Update strategies:
    // - Append: for an existing pull request, take the source branch and add new
    //   commits on top.
    // - Replace: for an existing pull request, take the latest changes from the
    //   target branch, make commits on top, and force push to the source branch.
    //
    // When no pull request exists, the source branch is reset to the state of
    // the target branch no matter which strategy is selected.
    updateStrategy: PullRequestUpdateStrategy.Append,

    // - Proceed: ignore commits that weren't authored by this automation and
    //   continue with the chosen update strategy.
    // - Stop: refuse to make changes to a PR if it contains commits that
    //   weren't authored by this automation.
    onForeignCommits: ForeignCommitPolicy.Proceed,

    cancellationToken: ct
);
```

### Dependency injection

Register pull request automation with a git identity and fixed token. The
resulting `PullRequestManager` can be injected into application services:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddPullRequestAutomation(
    identity: new AutomationIdentity("bot", "bot@example.com"),
    token: Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "");
```

For more control, register each dependency directly:

```csharp
services.AddLogging(builder => builder.AddSimpleConsole());
services.AddSingleton<IProcessRunner, CustomProcessRunner>();
services.AddSingleton<IGitAccessTokenProvider, GitHubAppTokenProvider>();
services.AddSingleton(new AutomationIdentity("bot", "bot@example.com"));
services.AddSingleton<PullRequestManager>();
```

`IGitAccessTokenProvider` is queried before each operation, so implementations
can refresh credentials such as GitHub App installation tokens.
