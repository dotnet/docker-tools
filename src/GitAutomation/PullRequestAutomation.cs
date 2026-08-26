// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitAutomation.AzureDevOps;
using Microsoft.DotNet.GitAutomation.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Creates pull request managers for supported repository hosts.
/// </summary>
public static class PullRequestAutomation
{
    /// <summary>
    /// Creates a GitHub pull request manager with a caller-provided access provider.
    /// </summary>
    /// <param name="accessProvider">Provides credentials and identity for GitHub operations.</param>
    /// <param name="loggerFactory">Creates loggers, or <see langword="null"/> to disable logging.</param>
    /// <returns>A pull request manager for GitHub repositories.</returns>
    public static PullRequestManager<GitHubRepo> ForGitHub(
        IGitHubAccessProvider accessProvider,
        ILoggerFactory? loggerFactory = null)
    {
        ILoggerFactory effectiveLoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        IProcessRunner processRunner = new ProcessRunner(effectiveLoggerFactory.CreateLogger<ProcessRunner>());
        return ForGitHub(accessProvider, processRunner, effectiveLoggerFactory);
    }

    /// <summary>
    /// Creates a GitHub pull request manager with caller-provided services.
    /// </summary>
    /// <param name="accessProvider">Provides credentials and identity for GitHub operations.</param>
    /// <param name="processRunner">Runs git processes.</param>
    /// <param name="loggerFactory">Creates loggers.</param>
    /// <returns>A pull request manager for GitHub repositories.</returns>
    public static PullRequestManager<GitHubRepo> ForGitHub(
        IGitHubAccessProvider accessProvider,
        IProcessRunner processRunner,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(accessProvider);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Git git = new(processRunner, loggerFactory.CreateLogger(nameof(Git)));
        var repoHostProvider = new GitHubRepoHostProvider(accessProvider, loggerFactory, git);
        return new(repoHostProvider, git, loggerFactory);
    }

    /// <summary>
    /// Creates a GitHub pull request manager with a fixed token.
    /// </summary>
    /// <param name="token">An access token with permission to push and manage pull requests.</param>
    /// <param name="identity">The git identity used for automation commits.</param>
    /// <param name="loggerFactory">Creates loggers, or <see langword="null"/> to disable logging.</param>
    /// <returns>A pull request manager for GitHub repositories.</returns>
    public static PullRequestManager<GitHubRepo> ForGitHub(
        string token,
        AutomationIdentity identity,
        ILoggerFactory? loggerFactory = null) =>
        ForGitHub(new StaticGitHubAccessProvider(token, identity), loggerFactory);

    /// <summary>
    /// Creates an Azure DevOps pull request manager with a caller-provided access provider.
    /// </summary>
    /// <param name="accessProvider">Provides credentials and identity for Azure DevOps operations.</param>
    /// <param name="loggerFactory">Creates loggers, or <see langword="null"/> to disable logging.</param>
    /// <returns>A pull request manager for Azure DevOps repositories.</returns>
    public static PullRequestManager<AzureDevOpsRepo> ForAzureDevOps(
        IAzureDevOpsAccessProvider accessProvider,
        ILoggerFactory? loggerFactory = null)
    {
        ILoggerFactory effectiveLoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        IProcessRunner processRunner = new ProcessRunner(effectiveLoggerFactory.CreateLogger<ProcessRunner>());
        return ForAzureDevOps(accessProvider, processRunner, effectiveLoggerFactory);
    }

    /// <summary>
    /// Creates an Azure DevOps pull request manager with caller-provided services.
    /// </summary>
    /// <param name="accessProvider">Provides credentials and identity for Azure DevOps operations.</param>
    /// <param name="processRunner">Runs git processes.</param>
    /// <param name="loggerFactory">Creates loggers.</param>
    /// <returns>A pull request manager for Azure DevOps repositories.</returns>
    public static PullRequestManager<AzureDevOpsRepo> ForAzureDevOps(
        IAzureDevOpsAccessProvider accessProvider,
        IProcessRunner processRunner,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(accessProvider);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Git git = new(processRunner, loggerFactory.CreateLogger(nameof(Git)));
        var repoHostProvider = new AzureDevOpsRepoHostProvider(accessProvider, loggerFactory, git);
        return new(repoHostProvider, git, loggerFactory);
    }

    /// <summary>
    /// Creates an Azure DevOps pull request manager with a fixed token.
    /// </summary>
    /// <param name="token">An access token with permission to push and manage pull requests.</param>
    /// <param name="identity">The git identity used for automation commits.</param>
    /// <param name="authenticationType">The token's authentication scheme.</param>
    /// <param name="loggerFactory">Creates loggers, or <see langword="null"/> to disable logging.</param>
    /// <returns>A pull request manager for Azure DevOps repositories.</returns>
    public static PullRequestManager<AzureDevOpsRepo> ForAzureDevOps(
        string token,
        AutomationIdentity identity,
        AzureDevOpsAuthenticationType authenticationType = AzureDevOpsAuthenticationType.Bearer,
        ILoggerFactory? loggerFactory = null) =>
        ForAzureDevOps(new StaticAzureDevOpsAccessProvider(token, identity, authenticationType), loggerFactory);
}
