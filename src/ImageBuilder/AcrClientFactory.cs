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


public class AcrClientFactory(
    IAzureTokenCredentialProvider tokenCredentialProvider,
    IOptions<PublishConfiguration> publishConfigOptions,
    AcrRateLimiter rateLimiter
) : IAcrClientFactory
{
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly PublishConfiguration _publishConfig = publishConfigOptions.Value;
    private readonly AcrRateLimiter _rateLimiter = rateLimiter;

    public IAcrClient Create(string acrName)
    {
        var auth = _publishConfig.FindRegistryAuthentication(acrName);
        if (auth?.ServiceConnection is null)
        {
            throw new InvalidOperationException(
                $"No service connection found for ACR '{acrName}'. " +
                $"Ensure the ACR is configured in the publish configuration with a valid service connection.");
        }

        return Create(acrName, auth.ServiceConnection);
    }

    public IAcrClient Create(string acrName, IServiceConnection serviceConnection)
    {
        TokenCredential credential = _tokenCredentialProvider.GetCredential(serviceConnection);

        ContainerRegistryClientOptions options = new();
        options.AddPolicy(new AcrRateLimitingPolicy(_rateLimiter), HttpPipelinePosition.PerRetry);

        var client = new ContainerRegistryClient(DockerHelper.GetAcrUri(acrName), credential, options);
        return new AcrClientWrapper(client);
    }
}
