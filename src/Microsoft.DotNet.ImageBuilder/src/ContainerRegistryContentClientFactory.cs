// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;
using Azure.Containers.ContainerRegistry;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IContainerRegistryContentClientFactory))]
[method: ImportingConstructor]
internal class ContainerRegistryContentClientFactory(IAzureTokenCredentialProvider tokenCredentialProvider)
    : IContainerRegistryContentClientFactory
{
    public IContainerRegistryContentClient Create(
        string acrName,
        string repositoryName,
        IServiceConnection? serviceConnection)
    {
        var tokenCredential = tokenCredentialProvider.GetCredential(serviceConnection);
        return new ContainerRegistryContentClientWrapper(
            new ContainerRegistryContentClient(
                DockerHelper.GetAcrUri(acrName),
                repositoryName,
                tokenCredential));
    }
}
