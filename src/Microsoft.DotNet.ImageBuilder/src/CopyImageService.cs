// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
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
        string subscription, string resourceGroup, ServicePrincipalOptions servicePrincipalOptions, string[] destTagNames,
        string destRegistryName, string srcTagName, string? srcRegistryName = null, ResourceIdentifier? srcResourceId = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null, bool isDryRun = false);
}

[Export(typeof(ICopyImageService))]
public class CopyImageService : ICopyImageService
{
    private readonly ILoggerService _loggerService;

    [ImportingConstructor]
    public CopyImageService(ILoggerService loggerService)
    {
        _loggerService = loggerService;
    }

    public static string GetBaseRegistryName(string registry) => registry.TrimEnd(".azurecr.io");

    public async Task ImportImageAsync(
        string subscription, string resourceGroup, ServicePrincipalOptions servicePrincipalOptions, string[] destTagNames,
        string destRegistryName, string srcTagName, string? srcRegistryName = null, ResourceIdentifier? srcResourceId = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null, bool isDryRun = false)
    {
        destRegistryName = GetBaseRegistryName(destRegistryName);

        ClientSecretCredential credentials = new(servicePrincipalOptions.Tenant, servicePrincipalOptions.ClientId, servicePrincipalOptions.Secret);
        ArmClient client = new(credentials);
        ContainerRegistryResource registryResource = client.GetContainerRegistryResource(
            ContainerRegistryResource.CreateResourceIdentifier(subscription, resourceGroup, destRegistryName));
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

        string formattedDestTagNames = string.Join(", ", destTagNames.Select(tag => $"'{DockerHelper.GetImageName(destRegistryName, tag)}'").ToArray());
        _loggerService.WriteMessage($"Importing {formattedDestTagNames} from '{DockerHelper.GetImageName(srcRegistryName, srcTagName)}'");

        if (!isDryRun)
        {
            try
            {
                await RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => registryResource.ImportImageAsync(WaitUntil.Completed, importImageContent));
            }
            catch (Exception e)
            {
                string errorMsg = $"Importing Failure: {formattedDestTagNames}";
                errorMsg += Environment.NewLine + e.ToString();

                _loggerService.WriteMessage(errorMsg);

                throw;
            }
        }
    }
}
#nullable disable
