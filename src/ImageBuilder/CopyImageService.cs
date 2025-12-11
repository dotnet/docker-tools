// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface ICopyImageService
{
    Task ImportImageAsync(
        string subscription,
        string resourceGroup,
        string[] destTagNames,
        string destRegistryName,
        string srcTagName,
        string? srcRegistryName = null,
        ResourceIdentifier? srcResourceId = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null,
        bool isDryRun = false);
}

public class CopyImageService : ICopyImageService
{
    private readonly ILoggerService _loggerService;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
    private readonly PublishConfiguration _publishConfig;
    private readonly ConcurrentDictionary<string, ArmClient> _armClientCache = new();

    public CopyImageService(
        ILoggerService loggerService,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        IOptions<PublishConfiguration> publishConfigOptions)
    {
        _loggerService = loggerService;
        _tokenCredentialProvider = tokenCredentialProvider;
        _publishConfig = publishConfigOptions.Value;
    }

    public async Task ImportImageAsync(
        string subscription,
        string resourceGroup,
        string[] destTagNames,
        string destAcrName,
        string srcTagName,
        string? srcRegistryName = null,
        ResourceIdentifier? srcResourceId = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null,
        bool isDryRun = false)
    {
        Acr destAcr = Acr.Parse(destAcrName);

        ContainerRegistryImportSource importSrc = new(srcTagName)
        {
            ResourceId = srcResourceId,
            RegistryAddress = srcRegistryName,
            Credentials = sourceCredentials
        };

        ContainerRegistryImportImageContent importImageContent = new(importSrc)
        {
            Mode = ContainerRegistryImportMode.Force
        };

        importImageContent.TargetTags.AddRange(destTagNames);

        string action = isDryRun ? "(Dry run) Would have imported" : "Importing";
        string sourceImageName = DockerHelper.GetImageName(srcRegistryName, srcTagName);
        var destinationImageNames = destTagNames
            .Select(tag => $"'{DockerHelper.GetImageName(destAcr.Name, tag)}'")
            .ToList();
        string formattedDestinationImages = string.Join(", ", destinationImageNames);
        _loggerService.WriteMessage($"{action} {formattedDestinationImages} from '{sourceImageName}'");

        if (!isDryRun)
        {
            ArmClient armClient = GetArmClientForAcr(destAcrName);
            ContainerRegistryResource registryResource = armClient.GetContainerRegistryResource(
                ContainerRegistryResource.CreateResourceIdentifier(subscription, resourceGroup, destAcr.Name));

            try
            {
                await RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => registryResource.ImportImageAsync(WaitUntil.Completed, importImageContent));
            }
            catch (Exception e)
            {
                string errorMsg = $"Importing Failure: {formattedDestinationImages}";
                errorMsg += Environment.NewLine + e.ToString();

                _loggerService.WriteMessage(errorMsg);

                throw;
            }
        }
        else
        {
            _loggerService.WriteMessage("Importing skipped due to dry run.");
        }
    }

    private ArmClient GetArmClientForAcr(string acrName)
    {
        // Look up the service connection for this ACR from the publish configuration
        var acrConfig = _publishConfig.FindOwnedAcrByName(acrName);

        if (acrConfig?.ServiceConnection is null)
        {
            throw new InvalidOperationException(
                $"No service connection found for ACR '{acrName}'. " +
                $"Ensure the ACR is configured in the publish configuration with a valid service connection.");
        }

        // Cache ArmClient instances per service connection to avoid recreating them
        string cacheKey = string.Join('|', acrConfig.ServiceConnection.TenantId, acrConfig.ServiceConnection.ClientId);
        return _armClientCache.GetOrAdd(cacheKey, _ =>
        {
            TokenCredential credential = _tokenCredentialProvider.GetCredential(acrConfig.ServiceConnection);
            return new ArmClient(credential);
        });
    }
}
