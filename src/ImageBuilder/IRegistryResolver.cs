// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder;


/// <summary>
/// Represents the result of resolving a registry, including the effective registry endpoint,
/// any owned ACR configuration with a service connection, and explicit credentials from the command line.
/// </summary>
/// <param name="EffectiveRegistry">The actual API endpoint to use (e.g., DockerHubApiRegistry for Docker Hub).</param>
/// <param name="OwnedAcr">Non-null if we own this ACR and have a service connection for authentication.</param>
/// <param name="ExplicitCredentials">Fallback credentials explicitly passed in via command line.</param>
public record RegistryInfo(
    string EffectiveRegistry,
    RegistryConfiguration? OwnedAcr,
    RegistryCredentials? ExplicitCredentials);

/// <summary>
/// Resolves registry information, determining how to authenticate with a given registry.
/// This centralizes the logic for:
///   1. Detecting Docker Hub and mapping to its API endpoint.
///   2. Checking if a registry is a known ACR with a service connection.
///   3. Falling back to explicit credentials from the command line.
/// </summary>
public interface IRegistryResolver
{
    /// <summary>
    /// Resolves the given registry to determine the effective endpoint and authentication method.
    /// </summary>
    /// <param name="registry">The container registry to resolve.</param>
    /// <param name="credsHost">Optional host providing explicit credentials.</param>
    /// <returns>A <see cref="RegistryInfo"/> containing resolution results.</returns>
    RegistryInfo Resolve(string registry, IRegistryCredentialsHost? credsHost);
}
