// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitAutomation;
using Microsoft.DotNet.GitAutomation.GitHub;
using Microsoft.Extensions.Logging;

// Resolve command line args
if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: ImageBuilder.Updater <image-builder-reference>");
    return 1;
}
string imageBuilderRef = args[0];

// Setup services
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
ProcessRunner processRunner = new(loggerFactory.CreateLogger<ProcessRunner>());
CancellationToken cancellationToken = GetCancellationToken();
(AutomationIdentity identity, string gitHubToken) = await GetCredentialsAsync(processRunner, cancellationToken);
PullRequestManager pullRequestManager = new(gitHubToken, identity, loggerFactory);

// Pull image builder reference
await RunProcessAsync(processRunner, workingDirectory: null, "docker", ["pull", imageBuilderRef], cancellationToken);

(GitHubRepo Repository, string TargetBranch)[] subscriptions =
[
    (new("dotnet", "docker-tools"), "main"),
    (new("dotnet", "dotnet-docker"), "nightly"),

    // Temporarily disable stable repos during development.
    // (new("dotnet", "dotnet-docker"), "main"),
    // (new("dotnet", "dotnet-buildtools-prereqs-docker"), "main"),
    // (new("microsoft", "dotnet-framework-docker"), "main"),
    // (new("microsoft", "go-images"), "microsoft/main"),
    // (new("microsoft", "go-infra-images"), "main"),
];

const string PullRequestTitle = "Update common Docker engineering infrastructure with latest";
const string CommitMessage = "Update common Docker engineering infrastructure with latest";

foreach ((GitHubRepo repository, string targetBranch) in subscriptions)
{
    string key = $"{repository.Name}-{targetBranch}-update-docker-tools";
    string title = $"[{targetBranch}] {PullRequestTitle}";

    PullRequestDefinition definition = new(
        Key: key,
        Title: title,
        Body: $"Updates the common Docker engineering infrastructure from `{imageBuilderRef}`.",
        TargetBranch: targetBranch,
        ApplyChanges: async (git, ct) =>
        {
            await RunProcessAsync(
                processRunner,
                git.WorkspaceDirectory,
                "docker",
                [
                    "run",
                    "--rm",
                    "--volume", $"{git.WorkspaceDirectory}:/repo",
                    "--workdir", "/repo",
                    imageBuilderRef,
                    "update",
                    "--no-version-logging",
                    imageBuilderRef,
                ],
                ct
            );

            await git.CommitAsync(CommitMessage, ct);
        });

    PullRequestResult result = await pullRequestManager.CreateOrUpdateAsync(
        definition: definition,
        upstream: repository,
        fork: new GitHubRepo(identity.AuthorName, repository.Name),
        updateStrategy: PullRequestUpdateStrategy.Replace,
        onForeignCommits: ForeignCommitPolicy.Proceed,
        cancellationToken: cancellationToken);

    Console.WriteLine($"{repository.Owner}/{repository.Name} ({targetBranch}): {result.Action} {result.Url}");
}

return 0;

static CancellationToken GetCancellationToken()
{
    CancellationTokenSource cancellationTokenSource = new();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationTokenSource.Cancel();
    };
    return cancellationTokenSource.Token;
}

static async Task<(AutomationIdentity Identity, string Token)> GetCredentialsAsync(
    IProcessRunner processRunner,
    CancellationToken cancellationToken)
{
    string? gitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    string? gitHubUser = Environment.GetEnvironmentVariable("GITHUB_USER");
    string? gitHubEmail = Environment.GetEnvironmentVariable("GITHUB_EMAIL");

#if DEBUG
    // Use gh CLI to resolve auth when running locally
    if (string.IsNullOrWhiteSpace(gitHubToken))
        gitHubToken = await RunProcessAsync(
            processRunner,
            workingDirectory: null,
            fileName: "gh",
            arguments: ["auth", "token"],
            cancellationToken,
            writeOutput: false);

    if (string.IsNullOrWhiteSpace(gitHubUser))
        gitHubUser = await RunProcessAsync(
            processRunner,
            workingDirectory: null,
            fileName: "gh",
            arguments: ["api", "user", "--jq", ".login"],
            cancellationToken,
            writeOutput: false);

    if (string.IsNullOrWhiteSpace(gitHubEmail))
        gitHubEmail = await RunProcessAsync(
            processRunner,
            workingDirectory: null,
            fileName: "git",
            arguments: ["config", "user.email"],
            cancellationToken,
            writeOutput: false);
#endif

    ArgumentException.ThrowIfNullOrWhiteSpace(gitHubToken);
    ArgumentException.ThrowIfNullOrWhiteSpace(gitHubUser);
    ArgumentException.ThrowIfNullOrWhiteSpace(gitHubEmail);

    return (new AutomationIdentity(gitHubUser, gitHubEmail), gitHubToken);
}

static async Task<string> RunProcessAsync(
    IProcessRunner processRunner,
    string? workingDirectory,
    string fileName,
    IEnumerable<string> arguments,
    CancellationToken cancellationToken,
    bool writeOutput = true)
{
    ProcessResult result = await processRunner.RunAsync(
        workingDirectory,
        fileName,
        arguments,
        cancellationToken);

    if (writeOutput && !string.IsNullOrWhiteSpace(result.StandardOutput))
        Console.WriteLine(result.StandardOutput.TrimEnd());

    if (writeOutput && !string.IsNullOrWhiteSpace(result.StandardError))
        Console.Error.WriteLine(result.StandardError.TrimEnd());

    if (result.ExitCode != 0)
        throw new InvalidOperationException($"Process '{fileName}' exited with code {result.ExitCode}.");

    string output = result.StandardOutput.Trim();

    if (!writeOutput && string.IsNullOrWhiteSpace(output))
        throw new InvalidOperationException($"Process '{fileName}' did not produce the expected output.");

    return output;
}
