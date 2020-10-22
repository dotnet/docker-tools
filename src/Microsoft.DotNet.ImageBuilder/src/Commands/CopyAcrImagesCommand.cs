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
        private readonly Lazy<ImageArtifactDetails> _imageArtifactDetails;

        [ImportingConstructor]
        public CopyAcrImagesCommand(
            IAzureManagementFactory azureManagementFactory, ILoggerService loggerService)
            : base(azureManagementFactory, loggerService)
        {
            _imageArtifactDetails = new Lazy<ImageArtifactDetails>(() =>
            {
                if (!string.IsNullOrEmpty(Options.ImageInfoPath))
                {
                    return ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);
                }

                return null;
            });
        }

        public override async Task ExecuteAsync()
        {
            LoggerService.WriteHeading("COPYING IMAGES");

            string resourceId =
                $"/subscriptions/{Options.Subscription}/resourceGroups/{Options.ResourceGroup}/providers" +
                $"/Microsoft.ContainerRegistry/registries/{RegistryName}";

            IEnumerable<Task> importTasks = Manifest.FilteredRepos
                .Select(repo =>
                    repo.FilteredImages
                        .SelectMany(image => image.FilteredPlatforms)
                        .SelectMany(platform => GetTagInfos(repo, platform))
                        .Select(tagInfo =>
                            ImportImageAsync(
                                TrimRegistry(tagInfo.DestinationTag),
                                TrimRegistry(tagInfo.SourceTag),
                                srcResourceId: resourceId)))
                .SelectMany(tasks => tasks);

            await Task.WhenAll(importTasks);
        }

        private IEnumerable<(string SourceTag, string DestinationTag)> GetTagInfos(RepoInfo repo, PlatformInfo platform)
        {
            List<(string SourceTag, string DestinationTag)> tags =
                new List<(string SourceTag, string DestinationTag)>();

            // If an image info file was provided, use the tags defined there rather than the manifest. This is intended
            // to handle scenarios where the tag's value is dynamic, such as a timestamp, and we need to know the value
            // of the tag for the image that was actually built rather than just generating new tag values when parsing
            // the manifest.
            if (_imageArtifactDetails.Value != null)
            {
                RepoData repoData = _imageArtifactDetails.Value.Repos.FirstOrDefault(repoData => repoData.Repo == repo.Name);
                if (repoData != null)
                {
                    PlatformData platformData = repoData.Images
                        .SelectMany(image => image.Platforms)
                        .FirstOrDefault(platformData => platformData.Equals(platform));
                    if (platformData != null)
                    {
                        foreach (string tag in platformData.SimpleTags)
                        {
                            string destinationTag = TagInfo.GetFullyQualifiedName(repo.QualifiedName, tag);
                            string sourceTag = GetSourceTag(destinationTag);
                            tags.Add((sourceTag, destinationTag));

                            TagInfo tagInfo = platformData.PlatformInfo.Tags.First(tagInfo => tagInfo.Name == tag);
                            if (tagInfo.SyndicatedRepo != null)
                            {
                                destinationTag = TagInfo.GetFullyQualifiedName(
                                    $"{Manifest.Registry}/{Options.RepoPrefix}{tagInfo.SyndicatedRepo}",
                                    tag);
                                tags.Add((sourceTag, destinationTag));
                            }
                        }
                    }
                    else
                    {
                        LoggerService.WriteMessage($"Unable to find image info data for path '{platform.DockerfilePath}'.");
                    }
                }
                else
                {
                    LoggerService.WriteMessage($"Unable to find image info data for repo '{repo.Name}'.");
                }
            }
            else
            {
                tags.AddRange(platform.Tags
                    .Select(tag => (GetSourceTag(tag.FullyQualifiedName), tag.FullyQualifiedName)));
            }

            return tags;
        }

        private string TrimRegistry(string tag) => tag.TrimStart($"{Manifest.Registry}/");

        private string GetSourceTag(string destinationTag) =>
            destinationTag.Replace(Options.RepoPrefix, Options.SourceRepoPrefix);
    }
}
