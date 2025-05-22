// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IRegistryContentClientFactory
{
    IRegistryContentClient Create(
        string registry,
        string repo,
        string? ownedAcr = null,
        IServiceConnection? serviceConnection = null,
        IRegistryCredentialsHost? credsHost = null);
}
