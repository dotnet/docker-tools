// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.DockerTools.ImageBuilder;

#nullable enable
public interface IRegistryContentClientFactory
{
    IRegistryContentClient Create(
        string registry,
        string repo,
        string? ownedAcr = null,
        IRegistryCredentialsHost? credsHost = null);
}
