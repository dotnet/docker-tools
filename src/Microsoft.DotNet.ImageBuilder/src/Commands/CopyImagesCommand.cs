// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.Services;
using Polly;
using ImportSource = Microsoft.Azure.Management.ContainerRegistry.Fluent.Models.ImportSource;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class CopyImagesCommand<TOptions> : ManifestCommand<TOptions>
        where TOptions : CopyImagesOptions, new()
    {
        private readonly Lazy<string> registryName;

        public CopyImagesCommand(IAzureManagementFactory azureManagementFactory, ILoggerService loggerService)
        {
            this.AzureManagementFactory = azureManagementFactory;
            this.LoggerService = loggerService;

            this.registryName = new Lazy<string>(() => Manifest.Registry.TrimEnd(".azurecr.io"));
        }

        public string RegistryName => this.registryName.Value;

        public IAzureManagementFactory AzureManagementFactory { get; }
        public ILoggerService LoggerService { get; }

        protected async Task ImportImageAsync(string destTagName, string srcTagName, string srcRegistryName = null, string srcResourceId = null)
        {
            AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                Options.ServicePrincipal.ClientId,
                Options.ServicePrincipal.Secret,
                Options.ServicePrincipal.Tenant,
                AzureEnvironment.AzureGlobalCloud);
            IAzure azure = this.AzureManagementFactory.CreateAzureManager(credentials, Options.Subscription);

            ImportImageParametersInner importParams = new ImportImageParametersInner()
            {
                Mode = "Force",
                Source = new ImportSource(
                    srcTagName,
                    srcResourceId,
                    srcRegistryName),
                TargetTags = new string[] { destTagName }
            };

            this.LoggerService.WriteMessage($"Importing '{destTagName}' from '{srcTagName}'");

            if (!Options.IsDryRun)
            {
                try
                {
                    AsyncPolicy<HttpResponseMessage> policy = HttpPolicyBuilder.Create()
                        .WithMeteredRetryPolicy(this.LoggerService)
                        .Build();
                    await policy.ExecuteAsync(async () =>
                    {
                        await azure.ContainerRegistries.Inner.ImportImageAsync(Options.ResourceGroup, this.RegistryName, importParams);
                        return null;
                    });
                }
                catch (Exception e)
                {
                    this.LoggerService.WriteMessage($"Importing Failure: {destTagName}{Environment.NewLine}{e}");
                    throw;
                }
            }
        }
    }
}
