// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Polly;
using ImportSource = Microsoft.Azure.Management.ContainerRegistry.Fluent.Models.ImportSource;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyAcrImagesCommand : ManifestCommand<CopyAcrImagesOptions>
    {
        private readonly Lazy<ImageArtifactDetails> imageArtifactDetails;
        private readonly IAzureManagementFactory azureManagementFactory;
        private readonly IEnvironmentService environmentService;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public CopyAcrImagesCommand(
            IAzureManagementFactory azureManagementFactory, IEnvironmentService environmentService, ILoggerService loggerService) : base()
        {
            this.azureManagementFactory = azureManagementFactory ?? throw new ArgumentNullException(nameof(azureManagementFactory));
            this.environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.imageArtifactDetails = new Lazy<ImageArtifactDetails>(() =>
            {
                if (!String.IsNullOrEmpty(Options.ImageInfoPath))
                {
                    return ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);
                }

                return null;
            });
        }

        public override async Task ExecuteAsync()
        {
            this.loggerService.WriteHeading("COPYING IMAGES");

            string registryName = Manifest.Registry.TrimEnd(".azurecr.io");

            IEnumerable<Task> importTasks = Manifest.FilteredRepos
                .Select(repo =>
                    repo.FilteredImages
                        .SelectMany(image => image.FilteredPlatforms)
                        .SelectMany(platform => GetDestinationTagNames(repo, platform))
                        .Select(tag => ImportImageAsync(tag, registryName)))
                .SelectMany(tasks => tasks);

            await Task.WhenAll(importTasks);
        }

        private async Task ImportImageAsync(string destTagName, string registryName)
        {
            AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                Options.ServicePrincipal.ClientId,
                Options.ServicePrincipal.Secret,
                Options.ServicePrincipal.Tenant,
                AzureEnvironment.AzureGlobalCloud);
            IAzure azure = this.azureManagementFactory.CreateAzureManager(credentials, Options.Subscription);

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

            this.loggerService.WriteMessage($"Importing '{destTagName}' from '{sourceTagName}'");

            if (!Options.IsDryRun)
            {
                try
                {
                    AsyncPolicy<HttpResponseMessage> policy = HttpPolicyBuilder.Create()
                        .WithMeteredRetryPolicy(this.loggerService)
                        .Build();
                    await policy.ExecuteAsync(async () =>
                    {
                        await azure.ContainerRegistries.Inner.ImportImageAsync(Options.ResourceGroup, registryName, importParams);
                        return null;
                    });
                }
                catch (Exception e)
                {
                    this.loggerService.WriteMessage($"Importing Failure: {destTagName}{Environment.NewLine}{e}");
                    throw;
                }
            }
        }

        private IEnumerable<string> GetDestinationTagNames(RepoInfo repo, PlatformInfo platform)
        {
            IEnumerable<string> destTagNames = null;

            // If an image info file was provided, use the tags defined there rather than the manifest. This is intended
            // to handle scenarios where the tag's value is dynamic, such as a timestamp, and we need to know the value
            // of the tag for the image that was actually built rather than just generating new tag values when parsing
            // the manifest.
            if (imageArtifactDetails.Value != null)
            {
                RepoData repoData = imageArtifactDetails.Value.Repos.FirstOrDefault(repoData => repoData.Repo == repo.Name);
                if (repoData != null)
                {
                    PlatformData platformData = repoData.Images
                        .SelectMany(image => image.Platforms)
                        .FirstOrDefault(platformData => platformData.Equals(platform));
                    if (platformData != null)
                    {
                        destTagNames = platformData.SimpleTags
                            .Select(tag => TagInfo.GetFullyQualifiedName(repo.QualifiedName, tag));
                    }
                    else
                    {
                        this.loggerService.WriteError($"Unable to find image info data for path '{platform.DockerfilePath}'.");
                        this.environmentService.Exit(1);
                    }
                }
                else
                {
                    this.loggerService.WriteError($"Unable to find image info data for repo '{repo.Name}'.");
                    this.environmentService.Exit(1);
                }
            }
            else
            {
                destTagNames = platform.Tags
                    .Select(tag => tag.FullyQualifiedName);
            }

            destTagNames = destTagNames
                .Select(tag => tag.TrimStart($"{Manifest.Registry}/"));
            return destTagNames;
        }
    }
}
