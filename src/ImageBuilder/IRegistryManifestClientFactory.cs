// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IRegistryManifestClientFactory
{
    IRegistryManifestClient Create(
        string registry,
        string repo,
        string? ownedAcr = null,
        IServiceConnection? serviceConnection = null,
        IRegistryCredentialsHost? credsHost = null);
}
