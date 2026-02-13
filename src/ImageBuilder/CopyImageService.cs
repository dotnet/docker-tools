// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

public interface ICopyImageService
{
    Task ImportImageAsync(
        string[] destTagNames,
        string destAcrName,
        string srcTagName,
        string? srcRegistryName = null,
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
        string[] destTagNames,
        string destAcrName,
        string srcTagName,
        string? srcRegistryName = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null,
        bool isDryRun = false)
    {
        Acr destAcr = Acr.Parse(destAcrName);

        string action = isDryRun ? "(Dry run) Would have imported" : "Importing";
        string sourceImageName = DockerHelper.GetImageName(srcRegistryName, srcTagName);
        var destinationImageNames = destTagNames
            .Select(tag => $"'{DockerHelper.GetImageName(destAcr.Name, tag)}'")
            .ToList();
        string formattedDestinationImages = string.Join(", ", destinationImageNames);
        _loggerService.WriteMessage($"{action} {formattedDestinationImages} from '{sourceImageName}'");

        if (isDryRun)
        {
            _loggerService.WriteMessage("Importing skipped due to dry run.");
            return;
        }

        var destResourceId = _publishConfig.GetRegistryResource(destAcrName);
        var srcResourceId = srcRegistryName is not null
            ? _publishConfig.GetRegistryResource(srcRegistryName)
            : null;

        // Azure ACR import only supports one source identifier. Use ResourceId for ACR-to-ACR
        // imports (same tenant), or RegistryAddress for external registries.
        ContainerRegistryImportSource importSrc = new(srcTagName)
        {
            ResourceId = srcResourceId,
            RegistryAddress = srcResourceId is null ? srcRegistryName : null,
            Credentials = sourceCredentials
        };

        ContainerRegistryImportImageContent importImageContent = new(importSrc)
        {
            Mode = ContainerRegistryImportMode.Force
        };

        importImageContent.TargetTags.AddRange(destTagNames);

        ArmClient armClient = GetArmClientForAcr(destAcrName);
        ContainerRegistryResource registryResource = armClient.GetContainerRegistryResource(destResourceId);

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

    private ArmClient GetArmClientForAcr(string acrName)
    {
        // Look up the authentication for this ACR from the publish configuration
        var auth = _publishConfig.FindRegistryAuthentication(acrName);

        if (auth?.ServiceConnection is null)
        {
            throw new InvalidOperationException(
                $"No service connection found for ACR '{acrName}'. " +
                $"Ensure the ACR is configured in the publish configuration with a valid service connection.");
        }

        // Cache ArmClient instances per service connection to avoid recreating them
        string cacheKey = string.Join('|', auth.ServiceConnection.TenantId, auth.ServiceConnection.ClientId);
        return _armClientCache.GetOrAdd(cacheKey, _ =>
        {
            TokenCredential credential = _tokenCredentialProvider.GetCredential(auth.ServiceConnection);
            return new ArmClient(credential);
        });
    }
}
