// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Integration test for Microsoft.DotNet.ImageBuilder.Automation: runs one
// linear scenario through the public IRepoHost API against a real repository.
// All work happens on randomly named "automation-test/..." branches that are
// created at the start and deleted at the end, so the repository's own
// branches are never touched. Intended for private scratch repositories.

using System.Diagnostics;
using Microsoft.DotNet.ImageBuilder.Automation;
using Microsoft.Extensions.Logging;

if (args.Length == 0 || args[0] is not ("github" or "azdo"))
{
    PrintUsage();
    return 2;
}

Dictionary<string, string> options = [];
for (int i = 1; i < args.Length; i += 2)
{
    if (!args[i].StartsWith("--") || i + 1 >= args.Length)
    {
        Console.Error.WriteLine($"error: expected '--option value' pairs, got '{args[i]}'");
        PrintUsage();
        return 2;
    }

    options[args[i]] = args[i + 1];
}

string? Optional(string name) => options.GetValueOrDefault(name);

string Require(string name) =>
    Optional(name) ?? throw new ArgumentException($"missing required option '{name}'");

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole(console => console.SingleLine = true).SetMinimumLevel(LogLevel.Information));

var author = new GitAuthor(
    Optional("--author-name") ?? "dotnet-docker-bot",
    Optional("--author-email") ?? "dotnet-docker-bot@example.com");

string baseBranch = Optional("--base-branch") ?? "main";

Func<bool, IRepoHost> hostFactory;
Uri targetPushUrl;
Uri headPushUrl;
string token;

try
{
    if (args[0] == "github")
    {
        string owner = Require("--owner");
        string name = Require("--repo");
        string? headOwner = Optional("--head-owner");
        token = Optional("--token")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? throw new ArgumentException("missing '--token' (or GITHUB_TOKEN environment variable)");

        var repo = new GitHubRepo(owner, name);
        GitHubRepo? headRepo = headOwner is null ? null : new GitHubRepo(headOwner, name);
        hostFactory = isDryRun =>
            new GitHubRepoHost(repo, new GitAutomationOptions(token, author, isDryRun), headRepo, loggerFactory);
        targetPushUrl = GitHubPushUrl(owner, name, token);
        headPushUrl = GitHubPushUrl(headOwner ?? owner, name, token);
    }
    else
    {
        string organization = Require("--org");
        string project = Require("--project");
        string name = Require("--repo");
        token = Optional("--token")
            ?? Environment.GetEnvironmentVariable("AZDO_TOKEN")
            ?? throw new ArgumentException("missing '--token' (or AZDO_TOKEN environment variable)");

        var repo = new AzdoRepo(organization, project, name);
        hostFactory = isDryRun =>
            new AzdoRepoHost(repo, new GitAutomationOptions(token, author, isDryRun), loggerFactory);
        targetPushUrl = new Uri($"https://azdo:{Uri.EscapeDataString(token)}@dev.azure.com/{organization}/{project}/_git/{name}");
        headPushUrl = targetPushUrl;
    }
}
catch (ArgumentException e)
{
    Console.Error.WriteLine($"error: {e.Message}");
    PrintUsage();
    return 2;
}

return await new Scenario(hostFactory, targetPushUrl, headPushUrl, baseBranch, author, token).RunAsync();

static Uri GitHubPushUrl(string owner, string name, string token) =>
    new($"https://x-access-token:{Uri.EscapeDataString(token)}@github.com/{owner}/{name}");

static void PrintUsage() =>
    Console.Error.WriteLine("""
        Runs the ImageBuilder.Automation integration scenario against a real repository.
        Only randomly named 'automation-test/...' branches are created (and deleted afterwards);
        use a private scratch repository.

        Usage:
          dotnet run -- github --owner <owner> --repo <repo> [--head-owner <forkOwner>] [options]
          dotnet run -- azdo --org <org> --project <project> --repo <repo> [options]

        Options:
          --token <token>          Auth token. Defaults to GITHUB_TOKEN / AZDO_TOKEN.
          --base-branch <branch>   Branch to base the scratch branches on (default: main).
          --author-name <name>     Commit author used by the automation (default: dotnet-docker-bot).
          --author-email <email>   Commit author email (default: dotnet-docker-bot@example.com).
        """);

/// <summary>
/// The scenario: a fixed sequence of ensure operations whose expected
/// outcomes exercise create, idempotency, update, dry run, the
/// foreign-commit policies, and direct branch commits.
/// </summary>
internal sealed class Scenario(
    Func<bool, IRepoHost> hostFactory,
    Uri targetPushUrl,
    Uri headPushUrl,
    string baseBranch,
    GitAuthor author,
    string token)
{
    private static readonly GitAuthor s_human = new("Integration Human", "integration-human@example.com");

    private readonly string _runId = $"automation-test/{Guid.NewGuid():N}"[..32];
    private readonly Git _git = new(token);
    private int _failures;

    private string ScratchBranch => $"{_runId}/target";

    private string PullRequestKey => $"{_runId}/update";

    public async Task<int> RunAsync()
    {
        if (author.Name == s_human.Name)
        {
            Console.Error.WriteLine($"error: --author-name must differ from '{s_human.Name}'");
            return 2;
        }

        IRepoHost host = hostFactory(false);
        IRepoHost dryRunHost = hostFactory(true);
        string cloneDir = Path.Combine(Path.GetTempPath(), $"automation-integration-{Guid.NewGuid():N}");

        Console.WriteLine($"Run id: {_runId}");
        Console.WriteLine($"Scratch target branch: {ScratchBranch}");
        Console.WriteLine($"Pull request head branch: {PullRequestKey}");

        try
        {
            Console.WriteLine($"\n=== Scaffolding: push scratch branch '{ScratchBranch}' from '{baseBranch}' ===");
            await _git.RunAsync(null, "clone", "--single-branch", "--branch", baseBranch,
                targetPushUrl.AbsoluteUri, cloneDir);
            await _git.RunAsync(cloneDir, "push", targetPushUrl.AbsoluteUri, $"HEAD:refs/heads/{ScratchBranch}");

            await StepAsync("EnsureBranch with new content", EnsureOutcome.Updated,
                () => host.EnsureBranchAsync(Branch("branch content v1")),
                result => Expect(result.CommitSha is not null, "CommitSha should be set"));

            await StepAsync("EnsureBranch with the same content (idempotent)", EnsureOutcome.Unchanged,
                () => host.EnsureBranchAsync(Branch("branch content v1")));

            await StepAsync("EnsurePullRequest creates the pull request", EnsureOutcome.Created,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v1")),
                result => Expect(result.Url is not null, "Url should be set"));

            await StepAsync("EnsurePullRequest with the same content (idempotent)", EnsureOutcome.Unchanged,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v1")));

            await StepAsync("EnsurePullRequest with new content (Append)", EnsureOutcome.Updated,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v2")));

            await StepAsync("Dry run with newer content pushes nothing", EnsureOutcome.DryRun,
                () => dryRunHost.EnsurePullRequestAsync(PullRequest("pr content v3")));

            await StepAsync("Content from before the dry run is still in place", EnsureOutcome.Unchanged,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v2")));

            Console.WriteLine($"\n=== Scaffolding: push a commit by '{s_human.Name}' to '{PullRequestKey}' ===");
            await _git.RunAsync(cloneDir, "fetch", headPushUrl.AbsoluteUri, PullRequestKey);
            await _git.RunAsync(cloneDir, "checkout", "-B", PullRequestKey, "FETCH_HEAD");
            await _git.RunAsync(cloneDir,
                "-c", $"user.name={s_human.Name}", "-c", $"user.email={s_human.Email}",
                "commit", "--allow-empty", "-m", "Manual fix from a human");
            await _git.RunAsync(cloneDir, "push", headPushUrl.AbsoluteUri, $"HEAD:refs/heads/{PullRequestKey}");

            await StepAsync("Update is blocked by the human's commit (CommentAndStop)", EnsureOutcome.Stopped,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v3")),
                result => Expect(
                    result.Detail?.Contains(StopComment) == true,
                    "Detail should include the StopComment"));

            await StepAsync("A re-run is still blocked (and must not duplicate the comment)", EnsureOutcome.Stopped,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v3")));

            await StepAsync("Overwrite + Replace proceeds despite the human's commit", EnsureOutcome.Updated,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v3") with
                {
                    OnForeignCommits = ForeignCommitPolicy.Overwrite,
                    UpdateStrategy = PullRequestUpdateStrategy.Replace,
                }));

            await StepAsync("Final state is converged", EnsureOutcome.Unchanged,
                () => host.EnsurePullRequestAsync(PullRequest("pr content v3") with
                {
                    OnForeignCommits = ForeignCommitPolicy.Overwrite,
                    UpdateStrategy = PullRequestUpdateStrategy.Replace,
                }));
        }
        finally
        {
            await CleanUpAsync(cloneDir);
        }

        Console.WriteLine(_failures == 0
            ? "\nAll steps passed."
            : $"\n{_failures} step(s) FAILED.");
        Console.WriteLine("Note: verify on the pull request page that the stop comment was posted exactly once.");
        return _failures == 0 ? 0 : 1;
    }

    private const string StopComment = "This is an integration test; no action is needed.";

    private BranchSpec Branch(string content) => new()
    {
        Branch = ScratchBranch,
        CommitMessage = "Integration test: direct branch commit",
        Apply = repoRoot => File.WriteAllTextAsync(Path.Combine(repoRoot, "integration-test.txt"), content + "\n"),
    };

    private PullRequestSpec PullRequest(string content) => new()
    {
        Key = PullRequestKey,
        Title = $"[Integration test] {_runId}",
        Body = "Automated integration test of Microsoft.DotNet.ImageBuilder.Automation. Safe to close.",
        CommitMessage = "Integration test: pull request commit",
        TargetBranch = ScratchBranch,
        UpdateStrategy = PullRequestUpdateStrategy.Append,
        OnForeignCommits = ForeignCommitPolicy.CommentAndStop,
        StopComment = StopComment,
        Apply = repoRoot => File.WriteAllTextAsync(Path.Combine(repoRoot, "integration-test.txt"), content + "\n"),
    };

    private async Task StepAsync(
        string name,
        EnsureOutcome expected,
        Func<Task<EnsureResult>> action,
        Action<EnsureResult>? extraChecks = null)
    {
        Console.WriteLine($"\n=== {name} ===");
        EnsureResult result = await action();
        Console.WriteLine($"--> {result.Outcome}"
            + (result.Url is null ? "" : $", url: {result.Url}")
            + (result.CommitSha is null ? "" : $", commit: {result.CommitSha}")
            + (result.Detail is null ? "" : $"{Environment.NewLine}    detail: {result.Detail.ReplaceLineEndings($"{Environment.NewLine}    ")}"));

        Expect(result.Outcome == expected, $"expected outcome {expected}, got {result.Outcome}");
        if (result.Outcome == expected)
        {
            extraChecks?.Invoke(result);
        }
    }

    private void Expect(bool condition, string message)
    {
        if (!condition)
        {
            _failures++;
            Console.Error.WriteLine($"*** FAILED: {message}");
        }
    }

    private async Task CleanUpAsync(string cloneDir)
    {
        Console.WriteLine("\n=== Cleaning up ===");

        // Deleting the head branch first closes the pull request on GitHub.
        // Azure DevOps refuses to delete the source branch of an active pull
        // request, so a failure here means the pull request (and then the
        // branches) must be cleaned up manually.
        foreach ((Uri url, string branch) in new[] { (headPushUrl, PullRequestKey), (targetPushUrl, ScratchBranch) })
        {
            try
            {
                await _git.RunAsync(cloneDir, "push", url.AbsoluteUri, "--delete", branch);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"warning: could not delete branch '{branch}' — abandon the pull request and delete it manually."
                    + $" ({e.Message})");
            }
        }

        try
        {
            Directory.Delete(cloneDir, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"warning: could not delete temp clone '{cloneDir}': {e.Message}");
        }
    }
}

/// <summary>
/// Minimal git CLI runner for the test's own scaffolding (the library does
/// its git work internally). Masks the token in output.
/// </summary>
internal sealed class Git(string token)
{
    public async Task<string> RunAsync(string? workingDirectory, params string[] args)
    {
        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Console.WriteLine($"+ {Mask($"git {string.Join(' ', args)}")}");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git exited with code {process.ExitCode}: {Mask(await errorTask)}");
        }

        return (await outputTask).TrimEnd('\r', '\n');
    }

    private string Mask(string text) =>
        text.Replace(token, "***").Replace(Uri.EscapeDataString(token), "***");
}
