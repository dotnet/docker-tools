// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;


internal class AcrContentClientFactory(
    IAzureTokenCredentialProvider tokenCredentialProvider,
    IOptions<PublishConfiguration> publishConfigOptions)
    : IAcrContentClientFactory
{
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly PublishConfiguration _publishConfig = publishConfigOptions.Value;

    public IAcrContentClient Create(Acr acr, string repositoryName)
    {
        var auth = _publishConfig.FindRegistryAuthentication(acr.Server);
        if (auth?.ServiceConnection is null)
        {
            throw new InvalidOperationException(
                $"No service connection found for ACR '{acr.Server}'. " +
                $"Ensure the ACR is configured in the publish configuration with a valid service connection.");
        }

        return Create(acr, repositoryName, auth.ServiceConnection);
    }

    public IAcrContentClient Create(Acr acr, string repositoryName, IServiceConnection serviceConnection)
    {
        var tokenCredential = _tokenCredentialProvider.GetCredential(serviceConnection);
        var client = new ContainerRegistryContentClient(acr.RegistryUri, repositoryName, tokenCredential);
        return new AcrContentClientWrapper(client);
    }
}
