// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public partial class MergeImageInfoCommand : ManifestCommand<MergeImageInfoOptions, MergeImageInfoOptionsBuilder>
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

            // Keep track of initial state to identify updated images
            ImageArtifactDetails? initialImageArtifactDetails = null;

            ImageArtifactDetails targetImageArtifactDetails;
            if (Options.InitialImageInfoPath != null)
            {
                targetImageArtifactDetails = srcImageArtifactDetailsList.First(item => item.Path == Options.InitialImageInfoPath).ImageArtifactDetails;

                // Store a deep copy of the initial state for comparison if CommitUrlOverride is specified
                if (!string.IsNullOrEmpty(Options.CommitOverride))
                {
                    initialImageArtifactDetails = ImageInfoHelper.LoadFromContent(
                        JsonHelper.SerializeObject(targetImageArtifactDetails),
                        Manifest,
                        skipManifestValidation: Options.IsPublishScenario
                    );
                }

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

            // Apply CommitUrl override to updated images
            if (!string.IsNullOrEmpty(Options.CommitOverride) && initialImageArtifactDetails != null)
            {
                ApplyCommitOverrideToUpdatedImages(targetImageArtifactDetails, initialImageArtifactDetails, Options.CommitOverride);
            }

            string destinationContents = JsonHelper.SerializeObject(targetImageArtifactDetails) + Environment.NewLine;
            File.WriteAllText(Options.DestinationImageInfoPath, destinationContents);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Applies a commit URL override to platforms that have been updated
        /// since the initial state.
        /// </summary>
        /// <param name="current">
        /// The current merged image artifact details containing all platforms.
        /// This instance will be modified with the <see cref="commitOverride"/>
        /// </param>
        /// <param name="initial">
        /// The initial image artifact details used for comparison to detect
        /// updates.
        /// </param>
        /// <param name="commitOverride">
        /// This commit will be inserted into the CommitUrl of any platforms
        /// that were updated compared to the initial image info.
        /// </param>
        private static void ApplyCommitOverrideToUpdatedImages(
            ImageArtifactDetails current,
            ImageArtifactDetails initial,
            string commitOverride)
        {
            // If commitOverride does not contain a valid SHA, throw an error
            if (!CommitShaRegex.IsMatch(commitOverride))
            {
                throw new ArgumentException(
                    $"The commit override '{commitOverride}' is not a valid SHA.",
                    nameof(commitOverride));
            }

            foreach (RepoData currentRepo in current.Repos)
            {
                RepoData? initialRepo = initial.Repos
                    .FirstOrDefault(r => r.CompareTo(currentRepo) == 0);

                foreach (ImageData currentImage in currentRepo.Images)
                {
                    // Match images without relying on ImageData.CompareTo, which throws when
                    // ManifestImage is null.
                    //
                    // ManifestImage will be null when we're parsing a manifest file where the
                    // "initial" image was replaced or removed in the build where the "current"
                    // image was built. For example, this can happen when all the tags change for
                    // an image,
                    //
                    // This means if initialImage.ManifestImage is null, then we can essentially
                    // treat this currentImage as a new image.

                    ImageData? initialImage = null;
                    if (initialRepo is not null)
                    {
                        if (currentImage.ManifestImage is not null)
                        {
                            initialImage = initialRepo.Images
                                .FirstOrDefault(i => i.ManifestImage == currentImage.ManifestImage);
                        }

                        // By leaving initialImage null here, we are marking currentImage as a new
                        // image, since no initial image was found that matches it.
                    }

                    foreach (PlatformData currentPlatform in currentImage.Platforms)
                    {
                        PlatformData? initialPlatform = initialImage?.Platforms
                            .FirstOrDefault(p => p.CompareTo(currentPlatform) == 0);

                        // If platform doesn't exist in initial or has been updated (different digest or commit),
                        // override CommitUrl
                        if (initialPlatform is null
                            || initialPlatform.Digest != currentPlatform.Digest
                            || initialPlatform.CommitUrl != currentPlatform.CommitUrl)
                        {
                            if (!string.IsNullOrEmpty(currentPlatform.CommitUrl))
                            {
                                // Replace the commit SHA in the current URL with the one from the override
                                currentPlatform.CommitUrl =
                                    CommitShaRegex.Replace(currentPlatform.CommitUrl, commitOverride);
                            }
                        }
                    }
                }
            }
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

        [GeneratedRegex(@"[0-9a-f]{40}")]
        public static partial Regex CommitShaRegex { get; }
    }
}
