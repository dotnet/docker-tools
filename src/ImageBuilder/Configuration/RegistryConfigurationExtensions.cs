// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

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
}
