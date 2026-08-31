// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
}
