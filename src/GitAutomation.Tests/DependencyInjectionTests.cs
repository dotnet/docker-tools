// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitAutomation.AzureDevOps;
using Microsoft.DotNet.GitAutomation.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation.Tests;

[TestClass]
public sealed class DependencyInjectionTests
{
    [TestMethod]
    public void PullRequestManager_ResolvesWithDefaultServices()
    {
        ServiceCollection services = new();
        services.AddPullRequestAutomation();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        PullRequestManager manager = serviceProvider.GetRequiredService<PullRequestManager>();

        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void PullRequestManager_ResolvesWithCustomServices()
    {
        ServiceCollection services = new();
        bool processRunnerResolved = false;
        services.AddSingleton<IProcessRunner>(_ =>
        {
            processRunnerResolved = true;
            return new StubProcessRunner();
        });
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddPullRequestAutomation();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        PullRequestManager manager = serviceProvider.GetRequiredService<PullRequestManager>();

        Assert.IsNotNull(manager);
        Assert.IsTrue(processRunnerResolved);
    }

    [TestMethod]
    public void GitHubPullRequestEndpoint_ResolvesWithDefaultServices()
    {
        ServiceCollection services = new();
        StubGitHubAccessProvider accessProvider = new();

        services.AddGitHubPullRequestAutomation(
            accessProvider,
            new GitHubRepo("dotnet", "docker-tools"));

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GitHubPullRequestEndpoint endpoint = serviceProvider.GetRequiredService<GitHubPullRequestEndpoint>();
        PullRequestManager manager = serviceProvider.GetRequiredService<PullRequestManager>();
        IGitHubAccessProvider registeredAccessProvider =
            serviceProvider.GetRequiredService<IGitHubAccessProvider>();

        Assert.IsNotNull(endpoint);
        Assert.IsNotNull(manager);
        Assert.AreSame(accessProvider, registeredAccessProvider);
    }

    [TestMethod]
    public void GitHubPullRequestEndpoint_ResolvesWithAccessToken()
    {
        ServiceCollection services = new();

        services.AddGitHubPullRequestAutomation(
            new GitHubRepo("dotnet", "docker-tools"),
            new AutomationIdentity("Automation", "automation@example.com"),
            "access-token");

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GitHubPullRequestEndpoint endpoint = serviceProvider.GetRequiredService<GitHubPullRequestEndpoint>();
        IGitHubAccessProvider accessProvider = serviceProvider.GetRequiredService<IGitHubAccessProvider>();

        Assert.IsNotNull(endpoint);
        Assert.IsInstanceOfType<StaticGitHubAccessProvider>(accessProvider);
    }

    [TestMethod]
    public void AzureDevOpsPullRequestEndpoint_ResolvesWithDefaultServices()
    {
        ServiceCollection services = new();

        services.AddAzureDevOpsPullRequestAutomation(
            "organization",
            "project",
            "repository",
            new AutomationIdentity("Automation", "automation@example.com"),
            "access-token");

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        AzureDevOpsPullRequestEndpoint endpoint =
            serviceProvider.GetRequiredService<AzureDevOpsPullRequestEndpoint>();
        PullRequestManager manager = serviceProvider.GetRequiredService<PullRequestManager>();

        Assert.IsNotNull(endpoint);
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void PullRequestManager_CanBeCreatedWithoutDependencyInjection()
    {
        PullRequestManager manager = new PullRequestManager();

        Assert.IsNotNull(manager);
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }

    private sealed class StubGitHubAccessProvider : IGitHubAccessProvider
    {
        public ValueTask<GitHubAccess> GetAccessAsync(
            GitHubRepo repository,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
