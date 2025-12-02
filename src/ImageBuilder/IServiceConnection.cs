// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Represents an Azure DevOps service connection which can be used in an
/// AzurePipelinesCredential to authenticate with Azure resources in an Azure
/// pipeline.
/// </summary>
public interface IServiceConnection
{
    /// <summary>
    /// The Entra ID tenant that the service connection's Managed Identity lives in (GUID).
    /// </summary>
    /// <remarks>
    /// This can be found on the service connection's page in Azure DevOps under the "Edit" menu.
    /// </remarks>
    string TenantId { get; init; }

    /// <summary>
    /// The Client ID of the service connection's Managed Identity (GUID).
    /// </summary>
    /// <remarks>
    /// This can be found on the service connection's page in Azure DevOps under the "Edit" menu.
    /// </remarks>
    string ClientId { get; init; }

    /// <summary>
    /// The service connection ID (GUID).
    /// </summary>
    /// <remarks>
    /// This can be found in the URL of the service connection's page in Azure DevOps.
    /// </remarks>
    string Id { get; init; }
}
