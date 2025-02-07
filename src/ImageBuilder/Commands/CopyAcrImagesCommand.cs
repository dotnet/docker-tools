// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyAcrImagesCommand : CopyImagesCommand<CopyAcrImagesOptions, CopyAcrImagesOptionsBuilder>
    {
        private readonly Lazy<ImageArtifactDetails> _imageArtifactDetails;

        [ImportingConstructor]
        public CopyAcrImagesCommand(
            ICopyImageService copyImageService, ILoggerService loggerService)
            : base(copyImageService, loggerService)
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

        protected override string Description => "Copies the platform images as specified in the manifest between repositories of an ACR";

        public override async Task ExecuteAsync()
        {
            LoggerService.WriteHeading("COPYING IMAGES");

            if (!File.Exists(Options.ImageInfoPath))
            {
                LoggerService.WriteMessage(PipelineHelper.FormatWarningCommand(
                    "Image info file not found. Skipping image copy."));
                return;
            }

            ResourceIdentifier resourceId = ContainerRegistryResource.CreateResourceIdentifier(
                Options.Subscription, Options.ResourceGroup, CopyImageService.GetBaseAcrName(Options.SourceRegistry));

            IEnumerable<Task> importTasks = Manifest.FilteredRepos
                .Select(repo =>
                    repo.FilteredImages
                        .SelectMany(image => image.FilteredPlatforms)
                        .SelectMany(platform => GetTagInfos(repo, platform))
                        .Select(tagInfo =>
                            ImportImageAsync(
                                DockerHelper.TrimRegistry(tagInfo.DestinationTag, Manifest.Registry),
                                Manifest.Registry,
                                DockerHelper.TrimRegistry(tagInfo.SourceTag, Options.SourceRegistry),
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
                        .FirstOrDefault(platformData => platformData.PlatformInfo == platform);
                    if (platformData != null)
                    {
                        foreach (string tag in platformData.SimpleTags)
                        {
                            string destinationTag = TagInfo.GetFullyQualifiedName(repo.QualifiedName, tag);
                            string sourceTag = GetSourceTag(destinationTag);
                            tags.Add((sourceTag, destinationTag));

                            TagInfo tagInfo = platformData.PlatformInfo.Tags.FirstOrDefault(tagInfo => tagInfo.Name == tag);
                            // There may not be a matching tag due to dynamic tag names. For now, we'll say that
                            // syndication is not supported for dynamically named tags.
                            // See https://github.com/dotnet/docker-tools/issues/686
                            if (tagInfo?.SyndicatedRepo != null)
                            {
                                foreach (string syndicatedDestinationTagName in tagInfo.SyndicatedDestinationTags)
                                {
                                    destinationTag = TagInfo.GetFullyQualifiedName(
                                        $"{Manifest.Registry}/{Options.RepoPrefix}{tagInfo.SyndicatedRepo}",
                                        syndicatedDestinationTagName);
                                    tags.Add((sourceTag, destinationTag));
                                }
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

        private string GetSourceTag(string destinationTag) =>
            destinationTag
                .Replace(Manifest.Registry, Options.SourceRegistry)
                .Replace(Options.RepoPrefix, Options.SourceRepoPrefix);
    }
}
