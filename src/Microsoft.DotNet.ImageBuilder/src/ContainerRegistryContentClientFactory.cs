// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Containers.ContainerRegistry;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal class ContainerRegistryContentClientFactory : IContainerRegistryContentClientFactory
{
    public IContainerRegistryContentClient Create(string acrName, string repositoryName, TokenCredential credential) =>
        new ContainerRegistryContentClientWrapper(new ContainerRegistryContentClient(DockerHelper.GetAcrUri(acrName), repositoryName, credential));
}
