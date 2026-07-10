// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.Tests;

[TestClass]
public sealed class ProcessRunnerExtensionsTests
{
    [TestMethod]
    public async Task RunAsyncExtension_UsesCurrentWorkingDirectory()
    {
        StubProcessRunner processRunner = new(new ProcessResult(0, string.Empty, string.Empty));

        await processRunner.RunAsync("git", ["--version"], CancellationToken.None);

        Assert.IsNull(processRunner.WorkingDirectory);
    }

    private sealed class StubProcessRunner(ProcessResult result) : IProcessRunner
    {
        public string? WorkingDirectory { get; private set; }

        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            CancellationToken cancellationToken)
        {
            WorkingDirectory = workingDirectory;
            return Task.FromResult(result);
        }
    }
}
