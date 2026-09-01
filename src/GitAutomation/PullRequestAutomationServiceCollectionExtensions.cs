// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitAutomation.AzureDevOps;
using Microsoft.DotNet.GitAutomation.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Registers services for declarative pull request automation.
/// </summary>
public static class PullRequestAutomationServiceCollectionExtensions
{
    /// <summary>
    /// Registers pull request automation using caller-provided services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>A caller-provided <see cref="IProcessRunner"/> registration replaces the default.</remarks>
    public static IServiceCollection AddPullRequestAutomation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<PullRequestManager>();

        return services;
    }

    /// <summary>
    /// Registers pull request automation for a GitHub repository.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="accessProvider">Provides repository-specific GitHub access.</param>
    /// <param name="upstream">The repository that receives pull requests.</param>
    /// <param name="fork">The repository that receives source branches, or <see langword="null"/> to use the upstream.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddGitHubPullRequestAutomation(
        this IServiceCollection services,
        IGitHubAccessProvider accessProvider,
        GitHubRepo upstream,
        GitHubRepo? fork = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(accessProvider);

        services.AddPullRequestAutomation();
        services.AddSingleton(accessProvider);
        services.AddTransient(serviceProvider =>
        {
            var accessProvider = serviceProvider.GetRequiredService<IGitHubAccessProvider>();
            return new GitHubPullRequestEndpoint(accessProvider, upstream, fork);
        });

        return services;
    }

    /// <summary>
    /// Registers pull request automation for a GitHub repository using a static access token.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="upstream">The repository that receives pull requests.</param>
    /// <param name="identity">The git identity represented by the token.</param>
    /// <param name="accessToken">The GitHub access token.</param>
    /// <param name="fork">The repository that receives source branches, or <see langword="null"/> to use the upstream.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddGitHubPullRequestAutomation(
        this IServiceCollection services,
        GitHubRepo upstream,
        AutomationIdentity identity,
        string accessToken,
        GitHubRepo? fork = null)
    {
        var accessProvider = new StaticGitHubAccessProvider(accessToken, identity);
        return services.AddGitHubPullRequestAutomation(accessProvider, upstream, fork);
    }

    /// <summary>
    /// Registers pull request automation for an Azure DevOps repository.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="organization">Azure DevOps organization name.</param>
    /// <param name="project">Azure DevOps project name or ID.</param>
    /// <param name="repositoryIdOrName">Azure DevOps repository ID or name.</param>
    /// <param name="identity">The identity used for automation commits.</param>
    /// <param name="accessToken">Azure DevOps token used for REST API and Git access.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAzureDevOpsPullRequestAutomation(
        this IServiceCollection services,
        string organization,
        string project,
        string repositoryIdOrName,
        AutomationIdentity identity,
        string accessToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPullRequestAutomation();
        services.AddAzureDevOpsClient(organization, project, accessToken);
        services.AddTransient(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<AzureDevOpsClient>();
            return new AzureDevOpsPullRequestEndpoint(client, repositoryIdOrName, accessToken, identity);
        });

        return services;
    }
}
