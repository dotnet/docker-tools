// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.DotNet.GitAutomation.AzureDevOps;
using Microsoft.DotNet.GitAutomation.GitHub;

namespace Microsoft.DotNet.GitAutomation.Tests;

[TestClass]
public sealed class DependencyInjectionTests
{
    [TestMethod]
    public void PullRequestManager_ResolvesWithDefaultServices()
    {
        ServiceCollection services = new();
        var identity = new AutomationIdentity("bot", "bot@example.com");
        services.AddGitHubPullRequestAutomation(identity, "token");

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        PullRequestManager<GitHubRepo> manager = serviceProvider.GetRequiredService<PullRequestManager<GitHubRepo>>();

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

        services.AddSingleton<IGitHubAccessProvider>(_ =>
        {
            accessTokenProviderResolved = true;
            return new StaticGitHubAccessProvider("token", new AutomationIdentity("bot", "bot@example.com"));
        });

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddGitHubPullRequestAutomation();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        PullRequestManager<GitHubRepo> manager = serviceProvider.GetRequiredService<PullRequestManager<GitHubRepo>>();

        Assert.IsNotNull(manager);
        Assert.IsTrue(processRunnerResolved);
        Assert.IsTrue(accessTokenProviderResolved);
    }

    [TestMethod]
    public void PullRequestManager_CanBeCreatedWithoutDependencyInjection()
    {
        var identity = new AutomationIdentity("bot", "bot@example.com");
        PullRequestManager<GitHubRepo> manager = PullRequestAutomation.ForGitHub("token", identity);
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void GitHubAndAzureDevOpsManagers_CanBeRegisteredTogether()
    {
        ServiceCollection services = new();
        AutomationIdentity identity = new("bot", "bot@example.com");
        services.AddGitHubPullRequestAutomation(identity, "github-token");
        services.AddAzureDevOpsPullRequestAutomation(identity, "azure-devops-token");

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        Assert.IsNotNull(serviceProvider.GetRequiredService<PullRequestManager<GitHubRepo>>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<PullRequestManager<AzureDevOpsRepo>>());
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, string>? environmentVariables = null) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }
}
