// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record RegistryConfiguration
{
    /// <summary>
    /// The registry endpoint. Examples: "myregistry.azurecr.io",
    /// "mcr.microsoft.com/dotnet/sdk", "localhost:5000", etc.
    /// </summary>
    public string? Server { get; set; } = null;

    /// <summary>
    /// If the registry is an ACR, this is the Azure resource group that the ACR is in.
    /// </summary>
    public string? ResourceGroup { get; set; } = null;

    /// <summary>
    /// If the registry is an ACR, this is the Azure subscription that the ACR belongs to.
    /// </summary>
    public string? Subscription { get; set; } = null;

    /// <summary>
    /// The Azure DevOps service connection to use for authentication to the registry.
    /// </summary>
    public ServiceConnection? ServiceConnection { get; set; } = null;
}
