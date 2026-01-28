// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

/// <summary>
/// Represents the result of resolving a registry, including the effective registry endpoint,
/// any authentication configuration, and explicit credentials from the command line.
/// </summary>
/// <param name="EffectiveRegistry">The actual API endpoint to use (e.g., DockerHubApiRegistry for Docker Hub).</param>
/// <param name="RegistryAuthentication">Non-null if we have authentication configured for this registry (e.g., service connection for ACR).</param>
/// <param name="ExplicitCredentials">Fallback credentials explicitly passed in via command line.</param>
public record RegistryInfo(
    string EffectiveRegistry,
    RegistryAuthentication? RegistryAuthentication,
    RegistryCredentials? ExplicitCredentials);

/// <summary>
/// Resolves registry information, determining how to authenticate with a given registry.
/// This centralizes the logic for:
///   1. Detecting Docker Hub and mapping to its API endpoint.
///   2. Checking if a registry has authentication configured (e.g., service connection for ACR).
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
