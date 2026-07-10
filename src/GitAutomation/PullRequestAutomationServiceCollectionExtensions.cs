// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    public static IServiceCollection AddPullRequestAutomation(
        this IServiceCollection services,
        AutomationIdentity identity,
        string token)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(identity);

        services.TryAddSingleton<IGitAccessTokenProvider>(
            new StaticGitAccessTokenProvider(token));

        return services.AddPullRequestAutomation(identity);
    }

    /// <summary>
    /// Registers pull request automation using caller-provided services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="identity">The git identity used for automation commits.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// An <see cref="IGitAccessTokenProvider"/> must also be registered. A caller-provided
    /// <see cref="IProcessRunner"/> registration replaces the default <see cref="ProcessRunner"/>.
    /// </remarks>
    public static IServiceCollection AddPullRequestAutomation(
        this IServiceCollection services,
        AutomationIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(identity);

        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAddSingleton<ILogger<ProcessRunner>>(provider =>
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<ProcessRunner>());
        services.TryAddSingleton(identity);
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<PullRequestManager>();

        return services;
    }
}
