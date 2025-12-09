// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public static class ConfigurationExtensions
{
    public static void AddPublishConfiguration(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<PublishConfiguration>()
            .BindConfiguration(PublishConfiguration.ConfigurationKey);
    }

    /// <summary>
    /// Finds an ACR configuration by registry name that has a valid service connection.
    /// </summary>
    /// <param name="publishConfig">The publish configuration to search.</param>
    /// <param name="registryName">The registry name to look up (e.g., "myacr.azurecr.io" or "myacr").</param>
    /// <returns>The matching <see cref="RegistryConfiguration"/> with a service connection, or null if not found.</returns>
    public static RegistryConfiguration? FindAcrByName(this PublishConfiguration publishConfig, string registryName)
    {
        var targetAcr = Acr.Parse(registryName);
        return publishConfig.GetKnownAcrConfigurations().FirstOrDefault(config =>
            !string.IsNullOrWhiteSpace(config.Server)
            && config.ToAcr() == targetAcr
            && config.ServiceConnection is not null);
    }
}
