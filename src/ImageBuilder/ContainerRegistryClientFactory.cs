// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Containers.ContainerRegistry;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

public class ContainerRegistryClientFactory : IContainerRegistryClientFactory
{
    public IContainerRegistryClient Create(string acrName, TokenCredential credential) =>
        new ContainerRegistryClientWrapper(new ContainerRegistryClient(DockerHelper.GetAcrUri(acrName), credential));
}
