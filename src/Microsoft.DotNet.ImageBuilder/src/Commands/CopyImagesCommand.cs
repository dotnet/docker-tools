// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.Rest.Azure;
using ImportSource = Microsoft.Azure.Management.ContainerRegistry.Fluent.Models.ImportSource;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class CopyImagesCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
        where TOptions : CopyImagesOptions, new()
        where TOptionsBuilder : CopyImagesOptionsBuilder, new()
    {
        public CopyImagesCommand(IAzureManagementFactory azureManagementFactory, ILoggerService loggerService)
        {
            AzureManagementFactory = azureManagementFactory;
            LoggerService = loggerService;
        }

        public IAzureManagementFactory AzureManagementFactory { get; }
        public ILoggerService LoggerService { get; }

        protected string GetBaseRegistryName(string registry) => registry.TrimEnd(".azurecr.io");

        protected async Task ImportImageAsync(string destTagName, string destRegistryName, string srcTagName,
            string? srcRegistryName = null, string? srcResourceId = null, ImportSourceCredentials? sourceCredentials = null)
        {
            AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                Options.ServicePrincipal.ClientId,
                Options.ServicePrincipal.Secret,
                Options.ServicePrincipal.Tenant,
                AzureEnvironment.AzureGlobalCloud);
            IAzure azure = AzureManagementFactory.CreateAzureManager(credentials, Options.Subscription);

            ImportImageParametersInner importParams = new ImportImageParametersInner()
            {
                Mode = "Force",
                Source = new ImportSource(
                    srcTagName,
                    srcResourceId,
                    srcRegistryName,
                    sourceCredentials),
                TargetTags = new string[] { destTagName }
            };

            LoggerService.WriteMessage($"Importing '{destTagName}' from '{srcTagName}'");

            if (!Options.IsDryRun)
            {
                try
                {
                    await RetryHelper.GetWaitAndRetryPolicy<Exception>(LoggerService)
                        .ExecuteAsync(() => azure.ContainerRegistries.Inner.ImportImageAsync(Options.ResourceGroup, destRegistryName, importParams));
                }
                catch (Exception e)
                {
                    string errorMsg = $"Importing Failure: {destTagName}";
                    if (e is CloudException cloudException)
                    {
                        errorMsg += Environment.NewLine + cloudException.Body.Message;
                    }

                    errorMsg += Environment.NewLine + e.ToString();

                    LoggerService.WriteMessage(errorMsg);

                    throw;
                }
            }
        }
    }
}
#nullable disable
