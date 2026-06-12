// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Azure.Containers.ContainerRegistry;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.RateLimiting;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;


internal class AcrContentClientFactory(
    IAzureTokenCredentialProvider tokenCredentialProvider,
    IOptions<PublishConfiguration> publishConfigOptions,
    AcrRateLimiter rateLimiter)
    : IAcrContentClientFactory
{
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly PublishConfiguration _publishConfig = publishConfigOptions.Value;
    private readonly AcrRateLimiter _rateLimiter = rateLimiter;

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

        ContainerRegistryClientOptions options = new();
        options.AddPolicy(new AcrRateLimitingPolicy(_rateLimiter), HttpPipelinePosition.PerRetry);

        var client = new ContainerRegistryContentClient(acr.RegistryUri, repositoryName, tokenCredential, options);
        return new AcrContentClientWrapper(client);
    }
}
