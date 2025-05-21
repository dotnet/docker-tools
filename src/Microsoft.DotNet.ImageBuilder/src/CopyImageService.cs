// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Commands;
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

[Export(typeof(ICopyImageService))]
public class CopyImageService : ICopyImageService
{
    private readonly ILoggerService _loggerService;
    private readonly Lazy<ArmClient> _armClient;

    [ImportingConstructor]
    public CopyImageService(
        ILoggerService loggerService,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        ServiceConnectionOptions serviceConnection)
    {
        _loggerService = loggerService;
        _armClient = new Lazy<ArmClient>(() => new ArmClient(tokenCredentialProvider.GetCredential(serviceConnection)));
    }

    public static string GetBaseAcrName(string registry) => registry.TrimEndString(DockerHelper.AcrDomain);

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
        destAcrName = GetBaseAcrName(destAcrName);

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
            .Select(tag => $"'{DockerHelper.GetImageName(destAcrName, tag)}'")
            .ToList();
        string formattedDestinationImages = string.Join(", ", destinationImageNames);
        _loggerService.WriteMessage($"{action} {formattedDestinationImages} from '{sourceImageName}'");

        if (!isDryRun)
        {
            ContainerRegistryResource registryResource = _armClient.Value.GetContainerRegistryResource(
                ContainerRegistryResource.CreateResourceIdentifier(subscription, resourceGroup, destAcrName));

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
}
