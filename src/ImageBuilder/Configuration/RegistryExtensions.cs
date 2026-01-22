// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public static class RegistryEndpointExtensions
{
    /// <summary>
    /// Parses the registry endpoint server as an ACR reference.
    /// </summary>
    /// <returns>The ACR reference, or null if the server is not set.</returns>
    /// <remarks>
    /// This method does not validate whether the server is actually an ACR.
    /// </remarks>
    public static Acr? ToAcr(this RegistryEndpoint registryEndpoint)
    {
        if (string.IsNullOrWhiteSpace(registryEndpoint.Server))
        {
            return null;
        }

        return Acr.Parse(registryEndpoint.Server);
    }
}

public static class RegistryAuthenticationExtensions
{
    /// <summary>
    /// Determines if the authentication entry has a valid service connection for ACR access.
    /// </summary>
    /// <param name="auth">The registry authentication entry.</param>
    /// <param name="serviceConnection">The service connection if available.</param>
    /// <returns>True if a service connection is configured.</returns>
    public static bool HasServiceConnection(
        this RegistryAuthentication auth,
        [NotNullWhen(true)] out ServiceConnection? serviceConnection)
    {
        serviceConnection = auth.ServiceConnection;
        return serviceConnection is not null;
    }
}
