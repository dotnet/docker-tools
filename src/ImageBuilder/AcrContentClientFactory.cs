// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Containers.ContainerRegistry;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

internal class AcrContentClientFactory(IAzureTokenCredentialProvider tokenCredentialProvider)
    : IAcrContentClientFactory
{
    public IAcrContentClient Create(
        string acrName,
        string repositoryName,
        IServiceConnection? serviceConnection)
    {
        var tokenCredential = tokenCredentialProvider.GetCredential(
            serviceConnection,
            AzureScopes.ContainerRegistryScope);

        return new AcrContentClientWrapper(
            new ContainerRegistryContentClient(
                DockerHelper.GetAcrUri(acrName),
                repositoryName,
                tokenCredential));
    }
}
