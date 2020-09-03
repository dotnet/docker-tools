// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyAcrImagesCommand : CopyImagesCommand<CopyAcrImagesOptions>
    {
        private readonly Lazy<ImageArtifactDetails> imageArtifactDetails;
        private readonly IEnvironmentService environmentService;

        [ImportingConstructor]
        public CopyAcrImagesCommand(
            IAzureManagementFactory azureManagementFactory, ILoggerService loggerService, IEnvironmentService environmentService)
            : base(azureManagementFactory, loggerService)
        {
            this.environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
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
            this.LoggerService.WriteHeading("COPYING IMAGES");

            string resourceId =
                $"/subscriptions/{Options.Subscription}/resourceGroups/{Options.ResourceGroup}/providers" +
                $"/Microsoft.ContainerRegistry/registries/{RegistryName}";

            IEnumerable<Task> importTasks = Manifest.FilteredRepos
                .Select(repo =>
                    repo.FilteredImages
                        .SelectMany(image => image.FilteredPlatforms)
                        .SelectMany(platform => GetDestinationTagNames(repo, platform))
                        .Select(tag =>
                        {
                            string srcTagName = tag.Replace(Options.RepoPrefix, Options.SourceRepoPrefix);
                            return ImportImageAsync(tag, srcTagName, srcResourceId: resourceId);
                        }))
                .SelectMany(tasks => tasks);

            await Task.WhenAll(importTasks);
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
                        this.LoggerService.WriteError($"Unable to find image info data for path '{platform.DockerfilePath}'.");
                        this.environmentService.Exit(1);
                    }
                }
                else
                {
                    this.LoggerService.WriteError($"Unable to find image info data for repo '{repo.Name}'.");
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
