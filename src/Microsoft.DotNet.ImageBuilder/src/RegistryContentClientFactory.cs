﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using Azure.Containers.ContainerRegistry;
using Azure.Identity;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IRegistryContentClientFactory))]
[method: ImportingConstructor]
public class RegistryContentClientFactory(IHttpClientProvider httpClientProvider) : IRegistryContentClientFactory
{
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;

    [Import(AllowDefault = true, AllowRecomposition = true)]
    private IOptions _options = null!;

    // This constructor can be used for unit tests to pass an IOptions instance
    public RegistryContentClientFactory(IHttpClientProvider httpClientProvider, IOptions options)
        : this(httpClientProvider)
        => _options = options;

    public IRegistryContentClient Create(string registry, string repo, IRegistryCredentialsHost credsHost)
    {
        // Docker Hub's registry has a separate host name for its API
        string apiRegistry = registry == DockerHelper.DockerHubRegistry ?
            DockerHelper.DockerHubApiRegistry :
            registry!;

        string? ownedAcr = null;
        if (_options is DockerRegistryOptions dockerRegistryOptions)
        {
            ownedAcr = dockerRegistryOptions.RegistryOverride;
        }

        if (ownedAcr?.EndsWith(DockerHelper.AcrDomain) == false)
        {
            ownedAcr = $"{ownedAcr}{DockerHelper.AcrDomain}";
        }

        if (apiRegistry == ownedAcr)
        {
            // If the target registry is the owned ACR, connect to it with the Azure library API. This handles all the Azure auth.
            return new ContainerRegistryContentClientWrapper(
                new ContainerRegistryContentClient(new Uri($"https://{apiRegistry}"), repo, new DefaultAzureCredential()));
        }
        else
        {
            // Lookup the credentials, if any, for the registry where the image is located
            credsHost.Credentials.TryGetValue(registry, out RegistryCredentials? registryCreds);

            RegistryHttpClient httpClient = _httpClientProvider.GetRegistryClient();
            return new RegistryServiceClient(apiRegistry, repo, httpClient, registryCreds);
        }
    }
}
