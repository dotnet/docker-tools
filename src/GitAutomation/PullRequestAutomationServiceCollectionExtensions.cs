// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitAutomation.AzureDevOps;
using Microsoft.DotNet.GitAutomation.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Registers services for declarative pull request automation.
/// </summary>
public static class PullRequestAutomationServiceCollectionExtensions
{
    /// <summary>
    /// Registers pull request automation using a fixed access token and the default services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="identity">The git identity used for automation commits.</param>
    /// <param name="token">The fixed git access token.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddGitHubPullRequestAutomation(
        this IServiceCollection services,
        AutomationIdentity identity,
        string token)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(identity);

        var accessProvider = new StaticGitHubAccessProvider(token, identity);
        services.TryAddSingleton<IGitHubAccessProvider>(accessProvider);

        return services.AddGitHubPullRequestAutomation();
    }

    /// <summary>
    /// Registers pull request automation using caller-provided services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// An <see cref="IGitHubAccessProvider"/> must also be registered. A caller-provided
    /// <see cref="IProcessRunner"/> registration replaces the default <see cref="ProcessRunner"/>.
    /// </remarks>
    public static IServiceCollection AddGitHubPullRequestAutomation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton(serviceProvider =>
            PullRequestAutomation.ForGitHub(
                serviceProvider.GetRequiredService<IGitHubAccessProvider>(),
                serviceProvider.GetRequiredService<IProcessRunner>(),
                serviceProvider.GetRequiredService<ILoggerFactory>()));

        return services;
    }

    /// <summary>
    /// Registers Azure DevOps pull request automation using a fixed access token.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="identity">The git identity used for automation commits.</param>
    /// <param name="token">The fixed Azure DevOps access token.</param>
    /// <param name="authenticationType">The token's authentication scheme.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAzureDevOpsPullRequestAutomation(
        this IServiceCollection services,
        AutomationIdentity identity,
        string token,
        AzureDevOpsAuthenticationType authenticationType = AzureDevOpsAuthenticationType.Bearer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(identity);

        var accessProvider = new StaticAzureDevOpsAccessProvider(token, identity, authenticationType);
        services.TryAddSingleton<IAzureDevOpsAccessProvider>(accessProvider);

        return services.AddAzureDevOpsPullRequestAutomation();
    }

    /// <summary>
    /// Registers Azure DevOps pull request automation using caller-provided services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// An <see cref="IAzureDevOpsAccessProvider"/> must also be registered. A caller-provided
    /// <see cref="IProcessRunner"/> registration replaces the default <see cref="ProcessRunner"/>.
    /// </remarks>
    public static IServiceCollection AddAzureDevOpsPullRequestAutomation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton(serviceProvider =>
            PullRequestAutomation.ForAzureDevOps(
                serviceProvider.GetRequiredService<IAzureDevOpsAccessProvider>(),
                serviceProvider.GetRequiredService<IProcessRunner>(),
                serviceProvider.GetRequiredService<ILoggerFactory>()));

        return services;
    }
}
