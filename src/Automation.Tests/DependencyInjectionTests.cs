// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Automation.Tests;

[TestClass]
public sealed class DependencyInjectionTests
{
    [TestMethod]
    public void PullRequestManager_ResolvesWithDefaultServices()
    {
        ServiceCollection services = new();
        services.AddPullRequestAutomation(
            new AutomationIdentity("bot", "bot@example.com"),
            "token");

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        PullRequestManager manager = serviceProvider.GetRequiredService<PullRequestManager>();

        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void PullRequestManager_ResolvesWithCustomServices()
    {
        ServiceCollection services = new();
        bool processRunnerResolved = false;
        bool accessTokenProviderResolved = false;
        services.AddSingleton<IProcessRunner>(_ =>
        {
            processRunnerResolved = true;
            return new StubProcessRunner();
        });
        services.AddSingleton<IGitAccessTokenProvider>(_ =>
        {
            accessTokenProviderResolved = true;
            return new StaticGitAccessTokenProvider("token");
        });
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(new AutomationIdentity("bot", "bot@example.com"));
        services.AddSingleton<PullRequestManager>();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        PullRequestManager manager = serviceProvider.GetRequiredService<PullRequestManager>();

        Assert.IsNotNull(manager);
        Assert.IsTrue(processRunnerResolved);
        Assert.IsTrue(accessTokenProviderResolved);
    }

    [TestMethod]
    public void PullRequestManager_CanBeCreatedWithoutDependencyInjection()
    {
        PullRequestManager manager = new(
            "token",
            new AutomationIdentity("bot", "bot@example.com"));

        Assert.IsNotNull(manager);
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }
}
