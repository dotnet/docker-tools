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
    /// Finds authentication details for a registry by name.
    /// </summary>
    /// <param name="publishConfig">The publish configuration to search.</param>
    /// <param name="registryName">The registry name to look up (e.g., "myacr.azurecr.io" or "myacr").</param>
    /// <returns>The matching <see cref="RegistryAuthentication"/>, or null if not found.</returns>
    public static RegistryAuthentication? FindRegistryAuthentication(this PublishConfiguration publishConfig, string registryName)
    {
        // Try exact match first
        if (publishConfig.RegistryAuthentication.TryGetValue(registryName, out var auth))
        {
            return auth;
        }

        // Try normalized ACR name lookup (handle both "myacr" and "myacr.azurecr.io")
        var targetAcr = Acr.Parse(registryName);
        foreach (var (key, value) in publishConfig.RegistryAuthentication)
        {
            var keyAcr = Acr.Parse(key);
            if (keyAcr == targetAcr)
            {
                return value;
            }
        }

        return null;
    }
}
