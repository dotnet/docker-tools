// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

internal class AcrContentClientFactory(
    IAzureTokenCredentialProvider tokenCredentialProvider,
    IOptions<PublishConfiguration> publishConfigOptions)
    : IAcrContentClientFactory
{
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly PublishConfiguration _publishConfig = publishConfigOptions.Value;

    public IAcrContentClient Create(Acr acr, string repositoryName)
    {
        var acrConfig = _publishConfig.FindOwnedAcrByName(acr.Server);
        if (acrConfig?.ServiceConnection is null)
        {
            throw new InvalidOperationException(
                $"No service connection found for ACR '{acr.Server}'. " +
                $"Ensure the ACR is configured in the publish configuration with a valid service connection.");
        }

        var tokenCredential = _tokenCredentialProvider.GetCredential(acrConfig.ServiceConnection);

        var client = new ContainerRegistryContentClient(acr.RegistryUri, repositoryName, tokenCredential);
        var wrapper = new AcrContentClientWrapper(client);
        return wrapper;
    }
}
