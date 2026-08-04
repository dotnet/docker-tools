// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.DotNet.GitAutomation.GitHub;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation.Tests;

[TestClass]
public sealed class GitHubRepoHostTests
{
    [TestMethod]
    public async Task Push_UsesAuthorizationEnvironmentWithoutEmbeddingTokenInUrl()
    {
        const string Token = "test-value";
        GitHubRepo repo = new("dotnet", "example");
        StaticGitHubAccessProvider accessProvider = new(
            Token,
            new AutomationIdentity("Bot", "bot@example.com"));
        GitHubAccess access =
            await accessProvider.GetAccessAsync(repo, CancellationToken.None);
        RecordingProcessRunner processRunner = new();
        Git git = new(processRunner, NullLogger.Instance);
        GitHubRepoHost host = new(
            repo,
            repo,
            access,
            NullLoggerFactory.Instance,
            git);

        await host.ExecuteAsync(
            [new PushCommitsOperation("workspace", "automation/update", ForcePush: false)],
            CancellationToken.None);

        Assert.HasCount(3, processRunner.Arguments);
        Assert.IsTrue(processRunner.Arguments[0].Contains(
            "--config-env=http.extraHeader=GIT_AUTOMATION_AUTHORIZATION"));
        Assert.IsTrue(processRunner.Arguments[2].Contains(
            "--config-env=http.extraHeader=GIT_AUTOMATION_AUTHORIZATION"));
        Assert.IsFalse(processRunner.Arguments
            .SelectMany(arguments => arguments)
            .Any(argument => argument.Contains(Token, StringComparison.Ordinal)));
        Assert.AreEqual(
            $"AUTHORIZATION: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{Token}"))}",
            processRunner.EnvironmentVariables[0]["GIT_AUTOMATION_AUTHORIZATION"]);
        Assert.IsEmpty(processRunner.EnvironmentVariables[1]);
        Assert.IsFalse(processRunner.Arguments
            .SelectMany(arguments => arguments)
            .Any(argument =>
                argument.StartsWith("https://", StringComparison.Ordinal)
                && argument.Contains('@', StringComparison.Ordinal)));
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<string[]> Arguments { get; } = [];
        public List<IReadOnlyDictionary<string, string>> EnvironmentVariables { get; } = [];

        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The authenticated process overload should be used.");

        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            IReadOnlyDictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            string[] argumentArray = arguments.ToArray();
            Arguments.Add(argumentArray);
            EnvironmentVariables.Add(environmentVariables);
            string standardOutput = argumentArray.Contains("rev-parse")
                ? "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                : string.Empty;
            return Task.FromResult(new ProcessResult(0, standardOutput, string.Empty));
        }
    }
}
