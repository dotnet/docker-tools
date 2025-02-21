﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class MergeImageInfoCommand : ManifestCommand<MergeImageInfoOptions, MergeImageInfoOptionsBuilder>
    {
        protected override string Description => "Merges the content of multiple image info files into one file";

        public override Task ExecuteAsync()
        {
            IEnumerable<string> imageInfoFiles = Directory.EnumerateFiles(
                Options.SourceImageInfoFolderPath,
                "*.json",
                SearchOption.AllDirectories);

            List<(string Path, ImageArtifactDetails ImageArtifactDetails)> srcImageArtifactDetailsList = imageInfoFiles
                .OrderBy(file => file) // Ensure the files are ordered for testing consistency between OS's.
                .Select(imageDataPath =>
                    (imageDataPath, ImageInfoHelper.LoadFromFile(
                                        imageDataPath,
                                        Manifest,
                                        skipManifestValidation: Options.IsPublishScenario)))
                .ToList();

            if (!srcImageArtifactDetailsList.Any())
            {
                throw new InvalidOperationException(
                    $"No JSON files found in source folder '{Options.SourceImageInfoFolderPath}'");
            }

            ImageInfoMergeOptions options = new()
            {
                IsPublish = Options.IsPublishScenario
            };

            ImageArtifactDetails targetImageArtifactDetails;
            if (Options.InitialImageInfoPath != null)
            {
                targetImageArtifactDetails = srcImageArtifactDetailsList.First(item => item.Path == Options.InitialImageInfoPath).ImageArtifactDetails;

                if (Options.IsPublishScenario)
                {
                    RemoveOutOfDateContent(targetImageArtifactDetails);
                }
            }
            else
            {
                targetImageArtifactDetails = new ImageArtifactDetails();
            }

            foreach (ImageArtifactDetails srcImageArtifactDetails in
                srcImageArtifactDetailsList
                    .Select(item => item.ImageArtifactDetails)
                    .Where(details => details != targetImageArtifactDetails))
            {
                ImageInfoHelper.MergeImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails, options);
            }

            string destinationContents = JsonHelper.SerializeObject(targetImageArtifactDetails) + Environment.NewLine;
            File.WriteAllText(Options.DestinationImageInfoPath, destinationContents);

            return Task.CompletedTask;
        }

        private void RemoveOutOfDateContent(ImageArtifactDetails imageArtifactDetails)
        {
            for (int repoIndex = imageArtifactDetails.Repos.Count - 1; repoIndex >= 0; repoIndex--)
            {
                RepoData repoData = imageArtifactDetails.Repos[repoIndex];

                // Since the registry name is not represented in the image info, make sure to compare the repo name with the
                // manifest's repo model name which isn't registry-qualified.
                RepoInfo? manifestRepo = Manifest.AllRepos.FirstOrDefault(manifestRepo => manifestRepo.Name == repoData.Repo);

                // If there doesn't exist a matching repo in the manifest, remove it from the image info
                if (manifestRepo is null)
                {
                    imageArtifactDetails.Repos.Remove(repoData);
                    continue;
                }

                for (int imageIndex = repoData.Images.Count - 1; imageIndex >= 0; imageIndex--)
                {
                    ImageData imageData = repoData.Images[imageIndex];
                    ImageInfo manifestImage = imageData.ManifestImage;

                    // If there doesn't exist a matching image in the manifest, remove it from the image info
                    if (manifestImage is null)
                    {
                        repoData.Images.Remove(imageData);
                        continue;
                    }

                    for (int platformIndex = imageData.Platforms.Count - 1; platformIndex >= 0; platformIndex--)
                    {
                        PlatformData platformData = imageData.Platforms[platformIndex];
                        PlatformInfo? manifestPlatform = manifestImage.AllPlatforms
                            .FirstOrDefault(manifestPlatform => platformData.PlatformInfo == manifestPlatform);

                        // If there doesn't exist a matching platform in the manifest, remove it from the image info
                        if (manifestPlatform is null)
                        {
                            imageData.Platforms.Remove(platformData);
                        }
                    }
                }
            }

            if (imageArtifactDetails.Repos.Count == 0)
            {
                // Failsafe to prevent wiping out the image info due to a bug in the logic
                throw new InvalidOperationException(
                    "Removal of out-of-date content resulted in there being no content remaining in the target image info file. Something is probably wrong with the logic.");
            }
        }
    }
}
