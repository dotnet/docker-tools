// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Configuration;

/// <summary>
/// Represents a container registry endpoint without authentication details.
/// </summary>
/// <remarks>
/// This record holds the registry server address and any non-authentication metadata.
/// Authentication is handled separately via <see cref="RegistryAuthentication"/> entries
/// in <see cref="PublishConfiguration.RegistryAuthentication"/>.
/// </remarks>
public sealed record RegistryEndpoint
{
    /// <summary>
    /// The registry server address. Examples: "myregistry.azurecr.io",
    /// "mcr.microsoft.com", "ghcr.io", "localhost:5000".
    /// </summary>
    public string? Server { get; set; }
}
