// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;

namespace Microsoft.DotNet.DockerTools.ImageBuilder;

#nullable enable
public class ContainerRegistryClientWrapper(ContainerRegistryClient innerClient) : IContainerRegistryClient
{
    private readonly ContainerRegistryClient _innerClient = innerClient;

    public Task DeleteRepositoryAsync(string name) => _innerClient.DeleteRepositoryAsync(name);

    public IAsyncEnumerable<string> GetRepositoryNamesAsync() => _innerClient.GetRepositoryNamesAsync();

    public ContainerRepository GetRepository(string name) => _innerClient.GetRepository(name);
}
