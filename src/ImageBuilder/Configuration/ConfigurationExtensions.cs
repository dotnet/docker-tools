// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Linq;
using Azure.Core;
using Azure.ResourceManager.ContainerRegistry;
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

    public static void AddBuildConfiguration(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<BuildConfiguration>()
            .BindConfiguration(BuildConfiguration.ConfigurationKey);
    }

    /// <summary>
    /// Finds authentication details for a registry by name.
    /// </summary>
    /// <param name="publishConfig">The publish configuration to search.</param>
    /// <param name="registryName">The registry name to look up (e.g., "myacr.azurecr.io" or "myacr").</param>
    /// <returns>The matching <see cref="RegistryAuthentication"/>, or null if not found.</returns>
    public static RegistryAuthentication? FindRegistryAuthentication(
        this PublishConfiguration publishConfig,
        string registryName)
    {
        // Try normalized ACR name lookup (handles both "myacr" and "myacr.azurecr.io")
        var targetAcr = Acr.Parse(registryName);

        return publishConfig.RegistryAuthentication
            .FirstOrDefault(auth =>
                auth.Server is not null
                && Acr.Parse(auth.Server) == targetAcr);
    }

    public static ResourceIdentifier GetAcrResource(this PublishConfiguration publishConfig, string registry)
    {
        var registryAuthentication = publishConfig.FindRegistryAuthentication(registry)
            ?? throw new InvalidOperationException(
                $"No authentication details found for source registry '{registry}'."
                + $" Ensure the registry is configured in the publish configuration.");

        var subscription = registryAuthentication.Subscription
            ?? throw new InvalidOperationException(
                $"No subscription found for source registry '{registry}'."
                + $" Ensure the registry is configured in the publish configuration.");

        var resourceGroup = registryAuthentication.ResourceGroup
            ?? throw new InvalidOperationException(
                $"No resource group found for source registry '{registry}'."
                + $" Ensure the registry is configured in the publish configuration.");

        var resourceId =
            ContainerRegistryResource.CreateResourceIdentifier(
                subscriptionId: subscription,
                resourceGroupName: resourceGroup,
                registryName: Acr.Parse(registry).Name);

        return resourceId;
    }
}
