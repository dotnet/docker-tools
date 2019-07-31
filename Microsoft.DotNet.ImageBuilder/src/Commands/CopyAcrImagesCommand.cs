// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyAcrImagesCommand : ManifestCommand<CopyAcrImagesOptions>
    {
        private Lazy<RepoData[]> imageInfoRepos;

        public CopyAcrImagesCommand() : base()
        {
            this.imageInfoRepos = new Lazy<RepoData[]>(() =>
            {
                if (!String.IsNullOrEmpty(Options.ImageInfoPath))
                {
                    return JsonConvert.DeserializeObject<RepoData[]>(File.ReadAllText(Options.ImageInfoPath));
                }

                return null;
            });
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("COPYING IMAGES");

            string registryName = Manifest.Registry.TrimEnd(".azurecr.io");

            AzureCredentials credentials = SdkContext.AzureCredentialsFactory
                .FromServicePrincipal(Options.Username, Options.Password, Options.Tenant, AzureEnvironment.AzureGlobalCloud);
            IAzure azure = Azure.Management.Fluent.Azure
                .Configure()
                .Authenticate(credentials)
                .WithSubscription(Options.Subscription);

            IEnumerable<Task> importTasks = Manifest.FilteredRepos
                .Select(repo =>
                    repo.FilteredImages
                        .SelectMany(image => image.FilteredPlatforms)
                        .SelectMany(platform => GetDestinationTagNames(repo, platform))
                        .Select(tag => ImportImage(azure, tag, registryName)))
                .SelectMany(tasks => tasks);

            await Task.WhenAll(importTasks);
        }

        private async Task ImportImage(IAzure azure, string destTagName, string registryName)
        {
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
                    Logger.WriteMessage($"Importing Failure: {destTagName}{Environment.NewLine}{e}");
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
            if (imageInfoRepos.Value != null)
            {
                RepoData repoData = imageInfoRepos.Value.FirstOrDefault(repoData => repoData.Repo == repo.Model.Name);
                if (repoData != null)
                {
                    if (repoData.Images.TryGetValue(platform.BuildContextPath, out ImageData image))
                    {
                        destTagNames = image.SimpleTags
                            .Select(tag => TagInfo.GetFullyQualifiedName(repo.Name, tag));
                    }
                    else
                    {
                        Logger.WriteError($"Unable to find image info data for path '{platform.BuildContextPath}'.");
                        Environment.Exit(1);
                    }
                }
                else
                {
                    Logger.WriteError($"Unable to find image info data for repo '{repo.Model.Name}'.");
                    Environment.Exit(1);
                }
            }

            if (destTagNames == null)
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
