// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;

/// <inheritdoc />
internal class AcrImageImporter : IAcrImageImporter
{
    private readonly ILogger<AcrImageImporter> _logger;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
    private readonly PublishConfiguration _publishConfig;
    private readonly ConcurrentDictionary<string, ArmClient> _armClientCache = new();

    public AcrImageImporter(
        ILogger<AcrImageImporter> logger,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        IOptions<PublishConfiguration> publishConfigOptions)
    {
        _logger = logger;
        _tokenCredentialProvider = tokenCredentialProvider;
        _publishConfig = publishConfigOptions.Value;
    }

    /// <inheritdoc />
    public async Task ImportImageAsync(
        string destAcrName,
        ResourceIdentifier destResourceId,
        ContainerRegistryImportImageContent importContent)
    {
        var armClient = GetArmClientForAcr(destAcrName);
        var registryResource = armClient.GetContainerRegistryResource(destResourceId);

        try
        {
            await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                .ExecuteAsync(() => registryResource.ImportImageAsync(WaitUntil.Completed, importContent));
        }
        catch (Exception e)
        {
            var formattedTags = string.Join(", ", importContent.TargetTags);
            _logger.LogError(e, "Importing Failure: {DestinationTags}", formattedTags);
            throw;
        }
    }

    private ArmClient GetArmClientForAcr(string acrName)
    {
        var auth = _publishConfig.FindRegistryAuthentication(acrName);

        if (auth?.ServiceConnection is null)
        {
            throw new InvalidOperationException(
                $"No service connection found for ACR '{acrName}'. " +
                $"Ensure the ACR is configured in the publish configuration with a valid service connection.");
        }

        // Cache ArmClient instances per service connection to avoid recreating them
        var cacheKey = string.Join('|', auth.ServiceConnection.TenantId, auth.ServiceConnection.ClientId);
        return _armClientCache.GetOrAdd(cacheKey, _ =>
        {
            var credential = _tokenCredentialProvider.GetCredential(auth.ServiceConnection);
            return new ArmClient(credential);
        });
    }
}
