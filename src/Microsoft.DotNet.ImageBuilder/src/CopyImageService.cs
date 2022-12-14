// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.Rest.Azure;
using ImportSource = Microsoft.Azure.Management.ContainerRegistry.Fluent.Models.ImportSource;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface ICopyImageService
{
    Task ImportImageAsync(
        string subscription, string resourceGroup, ServicePrincipalOptions servicePrincipalOptions, string[] destTagNames,
        string destRegistryName, string srcTagName, string? srcRegistryName = null, string? srcResourceId = null,
        ImportSourceCredentials? sourceCredentials = null, bool isDryRun = false);
}

[Export(typeof(ICopyImageService))]
public class CopyImageService : ICopyImageService
{
    private readonly IAzureManagementFactory _azureManagementFactory;
    private readonly ILoggerService _loggerService;

    [ImportingConstructor]
    public CopyImageService(IAzureManagementFactory azureManagementFactory, ILoggerService loggerService)
    {
        _azureManagementFactory = azureManagementFactory;
        _loggerService = loggerService;
    }

    private static string GetBaseRegistryName(string registry) => registry.TrimEnd(".azurecr.io");

    public static string GetResourceId(string subscription, string resourceGroup, string registry) =>
        $"/subscriptions/{subscription}/resourceGroups/{resourceGroup}/providers" +
                $"/Microsoft.ContainerRegistry/registries/{GetBaseRegistryName(registry)}";

    public async Task ImportImageAsync(
        string subscription, string resourceGroup, ServicePrincipalOptions servicePrincipalOptions, string[] destTagNames,
        string destRegistryName, string srcTagName, string? srcRegistryName = null, string? srcResourceId = null,
        ImportSourceCredentials? sourceCredentials = null, bool isDryRun = false)
    {
        destRegistryName = GetBaseRegistryName(destRegistryName);

        AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
            servicePrincipalOptions.ClientId,
            servicePrincipalOptions.Secret,
            servicePrincipalOptions.Tenant,
            AzureEnvironment.AzureGlobalCloud);
        IAzure azure = _azureManagementFactory.CreateAzureManager(credentials, subscription);

        ImportImageParametersInner importParams = new()
        {
            Mode = "Force",
            Source = new ImportSource(
                srcTagName,
                srcResourceId,
                srcRegistryName,
                sourceCredentials),
            TargetTags = destTagNames
        };

        string formattedDestTagNames = string.Join(", ", destTagNames.Select(tag => $"'{DockerHelper.GetImageName(destRegistryName, tag)}'").ToArray());
        _loggerService.WriteMessage($"Importing {formattedDestTagNames} from '{DockerHelper.GetImageName(srcRegistryName, srcTagName)}'");

        if (!isDryRun)
        {
            try
            {
                await RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => azure.ContainerRegistries.Inner.ImportImageAsync(resourceGroup, destRegistryName, importParams));
            }
            catch (Exception e)
            {
                string errorMsg = $"Importing Failure: {formattedDestTagNames}";
                if (e is CloudException cloudException)
                {
                    errorMsg += Environment.NewLine + cloudException.Body.Message;
                }

                errorMsg += Environment.NewLine + e.ToString();

                _loggerService.WriteMessage(errorMsg);

                throw;
            }
        }
    }
}
#nullable disable
