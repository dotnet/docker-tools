// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation.Tests;

[TestClass]
public sealed class GitWorkingCopyTests
{
    [TestMethod]
    public async Task RemoteOperations_RefreshAuthorizationForEachOperation()
    {
        Uri remoteUrl = new Uri("https://github.com/dotnet/example");
        RecordingProcessRunner processRunner = new RecordingProcessRunner();
        int authorizationCallCount = 0;

        ValueTask<string> GetAuthorizationHeader(CancellationToken cancellationToken)
        {
            authorizationCallCount++;
            return ValueTask.FromResult($"TestScheme value-{authorizationCallCount}");
        }

        using GitWorkingCopy workingCopy = new GitWorkingCopy(
            processRunner,
            NullLogger.Instance);

        await workingCopy.CloneAsync(
            remoteUrl,
            GetAuthorizationHeader,
            "main",
            new AutomationIdentity("Bot", "bot@example.com"),
            CancellationToken.None);

        await workingCopy.PushAsync(
            remoteUrl,
            GetAuthorizationHeader,
            "update",
            force: false,
            CancellationToken.None);

        Assert.AreEqual(2, authorizationCallCount);
        Assert.AreEqual(2, processRunner.Environments.Count);
        Assert.AreEqual(
            $"http.{remoteUrl.AbsoluteUri}.extraheader",
            processRunner.Environments[0]["GIT_CONFIG_KEY_0"]);
        Assert.AreNotEqual(
            processRunner.Environments[0]["GIT_CONFIG_VALUE_0"],
            processRunner.Environments[1]["GIT_CONFIG_VALUE_0"]);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<IReadOnlyDictionary<string, string>> Environments { get; } = [];

        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken)
        {
            if (environment is not null)
            {
                Environments.Add(environment);
            }

            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}
