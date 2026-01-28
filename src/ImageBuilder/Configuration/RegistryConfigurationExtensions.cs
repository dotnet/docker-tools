// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public static class RegistryConfigurationExtensions
{
    // TODO: This should return null if the registry is not an ACR
    public static Acr? ToAcr(this RegistryConfiguration registryConfig)
    {
        if (string.IsNullOrWhiteSpace(registryConfig.Server))
        {
            return null;
        }

        return Acr.Parse(registryConfig.Server);
    }

    /// <summary>
    /// Determines if the registry is an Azure Container Registry that we can authenticate to.
    /// </summary>
    /// <returns>True if the registry is an Azure Container Registry that we can authenticate to.</returns>
    public static bool IsOwnedAcr(
        this RegistryConfiguration registry,
        [NotNullWhen(true)] out Acr? acr,
        [NotNullWhen(true)] out ServiceConnection? serviceConnection)
    {
        if (string.IsNullOrWhiteSpace(registry.Server) || registry.ServiceConnection is null)
        {
            acr = null;
            serviceConnection = null;
            return false;
        }

        acr = Acr.Parse(registry.Server);
        serviceConnection = registry.ServiceConnection;
        return true;
    }
}
