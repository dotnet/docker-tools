// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
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
// Keep these options in sync with ImageBuilder's logging configuration in src/ImageBuilder/ImageBuilder.cs.
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
}));

ProcessRunner processRunner = new(loggerFactory.CreateLogger<ProcessRunner>());
ILogger logger = loggerFactory.CreateLogger("ImageBuilder.Updater");
CancellationToken cancellationToken = GetCancellationToken();

IGitHubAccessProvider accessProvider = await GetAccessProviderAsync(processRunner, logger, cancellationToken);
PullRequestManager pullRequestManager = new PullRequestManager(processRunner, loggerFactory);

// Pull image builder reference
await RunProcessAsync(processRunner, workingDirectory: null, "docker", ["pull", imageBuilderRef], cancellationToken);

(GitHubRepo Repository, string TargetBranch)[] subscriptions =
[
    (new("dotnet", "docker-tools"), "main"),
    (new("dotnet", "dotnet-docker"), "nightly"),
    (new("dotnet", "dotnet-docker"), "main"),
    (new("dotnet", "dotnet-buildtools-prereqs-docker"), "main"),

    // Disable remaining microsoft org repos until GitHub App migration is complete
    // Tracked by https://github.com/dotnet/dotnet-docker-internal/issues/7468
    (new("microsoft", "dotnet-framework-docker"), "main"),
    // (new("microsoft", "go-images"), "microsoft/main"),
    // (new("microsoft", "go-infra-images"), "main"),
];

const string PullRequestTitle = "Update common Docker engineering infrastructure with latest";
string commitMessage = $"Update common Docker engineering infrastructure from {imageBuilderRef}";

List<UpdateOutcome> outcomes = [];

// Run updates for each repo
foreach ((GitHubRepo repository, string targetBranch) in subscriptions)
{
    cancellationToken.ThrowIfCancellationRequested();

    PullRequestDefinition definition = new(
        Key: $"dotnet-containers-bot/update-docker-tools-{targetBranch}",
        Title: $"[{targetBranch}] {PullRequestTitle}",
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

            await git.CommitAsync(commitMessage, ct);
        });

    try
    {
        GitHubPullRequestEndpoint gitHub = new GitHubPullRequestEndpoint(accessProvider, repository);

        PullRequestResult result = await pullRequestManager.CreateOrUpdateAsync(
            definition: definition,
            endpoint: gitHub,
            updateStrategy: PullRequestUpdateStrategy.Append,
            onForeignCommits: ForeignCommitPolicy.Proceed,
            cancellationToken: cancellationToken);

        string status = result.Url is null ? result.Action.ToString() : $"{result.Action} {result.Url}";
        outcomes.Add(new(repository, targetBranch, status, Succeeded: true));

        logger.LogInformation(
            "Update succeeded for {RepositoryOwner}/{RepositoryName} ({TargetBranch}): {Status}",
            repository.Owner,
            repository.Name,
            targetBranch,
            status);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        logger.LogError(
            exception,
            "Update failed for {RepositoryOwner}/{RepositoryName} ({TargetBranch}).",
            repository.Owner,
            repository.Name,
            targetBranch);
        outcomes.Add(new(repository, targetBranch, exception.Message, Succeeded: false));
    }
}

int failureCount = outcomes.Count(outcome => !outcome.Succeeded);
logger.LogInformation(
    "Completed updates: {SuccessCount} succeeded, {FailureCount} failed.",
    outcomes.Count - failureCount,
    failureCount);

foreach (UpdateOutcome outcome in outcomes)
{
    logger.LogInformation(
        "{Result}: {RepositoryOwner}/{RepositoryName} ({TargetBranch}): {Status}",
        outcome.Succeeded ? "Succeeded" : "Failed",
        outcome.Repository.Owner,
        outcome.Repository.Name,
        outcome.TargetBranch,
        outcome.Status);
}

return failureCount == 0 ? 0 : 1;

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

static async Task<IGitHubAccessProvider> GetAccessProviderAsync(
    IProcessRunner processRunner,
    ILogger logger,
    CancellationToken cancellationToken)
{
    // Accept explicit token first
    string? gitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (!string.IsNullOrEmpty(gitHubToken))
    {
        logger.LogInformation("Using GITHUB_TOKEN for authentication.");
        AutomationIdentity identity = new(GetEnvVar("GITHUB_USER"), GetEnvVar("GITHUB_EMAIL"));
        return new StaticGitHubAccessProvider(gitHubToken, identity);
    }

    // Accept GitHub App authentication second
    string? gitHubAppKeyUri = Environment.GetEnvironmentVariable("GITHUB_APP_KEY_URI");
    string? gitHubAppClientId = Environment.GetEnvironmentVariable("GITHUB_APP_CLIENT_ID");
    if (!string.IsNullOrEmpty(gitHubAppKeyUri) && !string.IsNullOrEmpty(gitHubAppClientId))
    {
        logger.LogInformation("Using GitHub App authentication for authentication.");
        Uri appKeyUri = new(gitHubAppKeyUri);
        var azureCredential = new AzureCliCredential();
        var cryptographyClient = new CryptographyClient(appKeyUri, azureCredential);
        return new GitHubAppAccessProvider(gitHubAppClientId, cryptographyClient);
    }

#if DEBUG
    // Use gh CLI to resolve auth when running locally
    logger.LogInformation("Using gh CLI authentication.");

    gitHubToken = await RunProcessAsync(
        processRunner,
        workingDirectory: null,
        fileName: "gh",
        arguments: ["auth", "token"],
        cancellationToken,
        writeOutput: false);

    string gitHubUser = await RunProcessAsync(
        processRunner,
        workingDirectory: null,
        fileName: "gh",
        arguments: ["api", "user", "--jq", ".login"],
        cancellationToken,
        writeOutput: false);

    string gitHubEmail = await RunProcessAsync(
        processRunner,
        workingDirectory: null,
        fileName: "git",
        arguments: ["config", "user.email"],
        cancellationToken,
        writeOutput: false);

    return new StaticGitHubAccessProvider(
        gitHubToken,
        new AutomationIdentity(gitHubUser, gitHubEmail));
#endif

    throw new InvalidOperationException(
        "Please set either [GITHUB_TOKEN] or "
        + "[GITHUB_APP_KEY_URI + GITHUB_APP_CLIENT_ID]");
}

static string GetEnvVar(string name) => Environment.GetEnvironmentVariable(name) switch
{
    null or "" => throw new InvalidOperationException($"Environment variable {name} is required"),
    string value => value,
};

sealed record UpdateOutcome(GitHubRepo Repository, string TargetBranch, string Status, bool Succeeded);
