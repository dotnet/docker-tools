// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Configuration;

/// <summary>
/// Authentication and Azure-specific metadata for a container registry.
/// </summary>
/// <remarks>
/// <para>
/// This record holds Azure-specific authentication details (ServiceConnection) and
/// ACR metadata (ResourceGroup, Subscription). For non-ACR registries, the ACR-specific
/// properties can be left null.
/// </para>
/// <para>
/// NOTE: This is distinct from <see cref="RegistryCredentials"/> which holds
/// username/password credentials for direct Docker registry authentication.
/// <see cref="RegistryAuthentication"/> is used for Azure service principal /
/// managed identity authentication via ServiceConnection.
/// </para>
/// </remarks>
public sealed record RegistryAuthentication
{
    /// <summary>
    /// The registry server address this authentication applies to.
    /// Examples: "myregistry.azurecr.io", "ghcr.io".
    /// </summary>
    public string? Server { get; set; }

    /// <summary>
    /// The Azure DevOps service connection for authenticating to this registry.
    /// Required for ACRs that we need to push to or manage.
    /// </summary>
    public ServiceConnection? ServiceConnection { get; set; }

    /// <summary>
    /// The Azure resource group containing this ACR. Null for non-ACR registries.
    /// </summary>
    public string? ResourceGroup { get; set; }

    /// <summary>
    /// The Azure subscription ID containing this ACR. Null for non-ACR registries.
    /// </summary>
    public string? Subscription { get; set; }
}
