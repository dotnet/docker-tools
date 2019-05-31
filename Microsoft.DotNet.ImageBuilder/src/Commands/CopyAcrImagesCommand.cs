// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyAcrImagesCommand : ManifestCommand<CopyAcrImagesOptions>
    {
        public CopyAcrImagesCommand() : base()
        {
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("COPING IMAGES");

            string registryName = Manifest.Registry.TrimEnd(".azurecr.io");

            await Task.WhenAll(Manifest.GetFilteredPlatformTags().Select(platformTag => ImportImage(platformTag, registryName)));
        }

        private async Task ImportImage(TagInfo platformTag, string registryName)
        {
            AzureCredentials credentials = SdkContext.AzureCredentialsFactory
                .FromServicePrincipal(Options.Username, Options.Password, Options.Tenant, AzureEnvironment.AzureGlobalCloud);
            IAzure azure = Microsoft.Azure.Management.Fluent.Azure
                .Configure()
                .Authenticate(credentials)
                .WithSubscription(Options.Subscription);

            string destTagName = platformTag.FullyQualifiedName.TrimStart($"{Manifest.Registry}/");
            string sourceTagName = destTagName.Replace(Options.RepoPrefix, Options.SourceRepoPrefix);
            ImportImageParametersInner importParams = new ImportImageParametersInner()
            {
                Mode = "Force",
                Source = new ImportSource(
                    sourceTagName,
                    $"/subscriptions/{Options.Subscription}/resourceGroups/{Options.ResourceGroup}/providers" +
                        $"/Microsoft.ContainerRegistry/registries/{registryName}"),
                TargetTags = new string[] { destTagName }
            };

            Logger.WriteMessage($"Importing '{destTagName}' from '{sourceTagName}'");

            if (!Options.IsDryRun)
            {
                try
                {
                    await azure.ContainerRegistries.Inner.ImportImageAsync(Options.ResourceGroup, registryName, importParams);
                }
                catch (Exception e)
                {
                    Logger.WriteMessage($"Importing Failure:  {destTagName}{Environment.NewLine}{e}");
                    throw;
                }
            }
        }
    }
}
