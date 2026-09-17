#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageInfoHelper
    {
        /// <summary>
        /// Overrides all digests in the ImageArtifactDetails with the given registry options.
        /// </summary>
        /// <param name="imageInfo"></param>
        /// <param name="overrideOptions"></param>
        /// <returns></returns>
        public static ImageArtifactDetails ApplyRegistryOverride(
            this ImageArtifactDetails imageInfo,
            RegistryOptions overrideOptions)
        {
            if (string.IsNullOrEmpty(overrideOptions.Registry)
                && string.IsNullOrEmpty(overrideOptions.RepoPrefix))
            {
                return imageInfo;
            }

            foreach (RepoData repo in imageInfo.Repos)
            {
                foreach (ImageData imageData in repo.Images)
                {
                    if (imageData.Manifest is not null
                        && !string.IsNullOrEmpty(imageData.Manifest.Digest))
                    {
                        imageData.Manifest.Digest =
                            overrideOptions.ApplyOverrideToDigest(imageData.Manifest.Digest, repoName: repo.Repo);
                    }

                    if (imageData.Manifest is not null)
                    {
                        for (int i = 0; i < imageData.Manifest.SyndicatedDigests.Count; i++)
                        {
                            string syndicatedDigest = imageData.Manifest.SyndicatedDigests[i];
                            if (!string.IsNullOrEmpty(syndicatedDigest))
                            {
                                string syndicatedRepo =
                                    DockerHelper.TrimRegistry(DockerHelper.GetRepo(syndicatedDigest));
                                imageData.Manifest.SyndicatedDigests[i] =
                                    overrideOptions.ApplyOverrideToDigest(syndicatedDigest, repoName: syndicatedRepo);
                            }
                        }
                    }

                    foreach (PlatformData platformData in imageData.Platforms)
                    {
                        if (!string.IsNullOrEmpty(platformData.Digest))
                        {
                            platformData.Digest =
                                overrideOptions.ApplyOverrideToDigest(platformData.Digest, repoName: repo.Repo);
                        }
                    }
                }
            }

            return imageInfo;
        }

        /// <summary>
        /// Gets all of the digests listed in the given image info.
        /// </summary>
        public static List<string> GetAllDigests(this ImageArtifactDetails imageInfo)
        {
            return imageInfo.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(GetAllDigests)
                .ToList();
        }

        public static List<string> GetAllDigests(this ImageData imageData)
        {
            // Platform specific digests
            IEnumerable<string> digests = imageData.Platforms.Select(platform => platform.Digest);

            // Include manifest list digest if it exists
            if (imageData.Manifest is not null)
            {
                digests =
                [
                    ..digests,
                    imageData.Manifest.Digest,
                    ..imageData.Manifest.SyndicatedDigests,
                ];
            }

            return digests.ToList();
        }

        public static List<ImageDigestInfo> GetAllImageDigestInfos(this ImageArtifactDetails imageInfo)
        {
            return imageInfo.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(GetAllImageDigestInfos)
                .ToList();
        }

        public static List<ImageDigestInfo> GetAllImageDigestInfos(this ImageData imageInfo)
        {
            List<ImageDigestInfo> imageDigestInfos = imageInfo.Platforms
                .Select(platform => new ImageDigestInfo(
                    Digest: platform.Digest,
                    Tags: platform.SimpleTags,
                    IsManifestList: false))
                .ToList();

            if (imageInfo.Manifest is not null)
            {
                imageDigestInfos.Add(new ImageDigestInfo(
                    Digest: imageInfo.Manifest.Digest,
                    Tags: imageInfo.Manifest.SharedTags,
                    IsManifestList: true));
            }

            return imageDigestInfos;
        }

        /// <summary>
        /// Loads an image info file as a parsed model directly with no validation or filtering.
        /// </summary>
        /// <param name="path">Path to the image info file.</param>
        /// <exception cref="InvalidDataException"/>
        public static ImageArtifactDetails DeserializeImageArtifactDetails(string path)
        {
            string imageInfoText = File.ReadAllText(path);
            return ImageArtifactDetails.FromJson(imageInfoText) ??
                throw new InvalidDataException($"Unable to deserialize image info file {path}");
        }

        /// <summary>
        /// Loads image info string content as a parsed model.
        /// </summary>
        /// <param name="imageInfoContent">The image info content to load.</param>
        /// <param name="manifest">Representation of the manifest model.</param>
        /// <param name="skipManifestValidation">
        /// Whether to skip validation if no associated manifest model item was found for a given image info model item.
        /// </param>
        /// <param name="useFilteredManifest">Whether to use the filtered content of the manifest for lookups.</param>
        public static ImageArtifactDetails LoadFromContent(string imageInfoContent, ManifestInfo manifest,
            bool skipManifestValidation = false, bool useFilteredManifest = false)
        {
            ImageArtifactDetails imageArtifactDetails = ImageArtifactDetails.FromJson(imageInfoContent);

            foreach (RepoData repoData in imageArtifactDetails.Repos)
            {
                RepoInfo manifestRepo = (useFilteredManifest ? manifest.FilteredRepos : manifest.AllRepos)
                    .FirstOrDefault(repo => repo.Name == repoData.Repo);
                if (manifestRepo == null)
                {
                    Console.WriteLine($"Image info repo not loaded: {repoData.Repo}");
                    continue;
                }

                foreach (ImageData imageData in repoData.Images)
                {
                    imageData.ManifestRepo = manifestRepo;

                    foreach (PlatformData platformData in imageData.Platforms)
                    {
                        foreach (ImageInfo manifestImage in useFilteredManifest ? manifestRepo.FilteredImages : manifestRepo.AllImages)
                        {
                            PlatformInfo matchingManifestPlatform = (useFilteredManifest ? manifestImage.FilteredPlatforms : manifestImage.AllPlatforms)
                                .FirstOrDefault(platform => ArePlatformsEqual(platformData, imageData, platform, manifestImage));
                            if (matchingManifestPlatform != null)
                            {
                                if (imageData.ManifestImage is null)
                                {
                                    imageData.ManifestImage = manifestImage;
                                }

                                platformData.PlatformInfo = matchingManifestPlatform;
                                platformData.ImageInfo = manifestImage;
                                break;
                            }
                        }
                    }

                    PlatformData representativePlatform = imageData.Platforms.FirstOrDefault();
                    if (!skipManifestValidation && imageData.ManifestImage == null && representativePlatform != null)
                    {
                        throw new InvalidOperationException(
                            $"Unable to find matching platform in manifest for platform '{representativePlatform.GetIdentifier()}'.");
                    }
                }
            }

            return imageArtifactDetails;
        }

        /// <summary>
        /// Loads an image info file as a parsed model.
        /// </summary>
        /// <param name="path">Path to the image info file.</param>
        /// <param name="manifest">Representation of the manifest model.</param>
        /// <param name="skipManifestValidation">
        /// Whether to skip validation if no associated manifest model item was found for a given image info model item.
        /// </param>
        /// <param name="useFilteredManifest">Whether to use the filtered content of the manifest for lookups.</param>
        public static ImageArtifactDetails LoadFromFile(string path, ManifestInfo manifest, bool skipManifestValidation = false, bool useFilteredManifest = false)
        {
            return LoadFromContent(File.ReadAllText(path), manifest, skipManifestValidation, useFilteredManifest);
        }

        /// <summary>
        /// Finds the <see cref="PlatformData"/> that matches the given <see cref="PlatformInfo"/>.
        /// </summary>
        /// <param name="platform">Platform being searched.</param>
        /// <param name="repo">Repo that corresponds to the platform.</param>
        /// <param name="imageArtifactDetails">Image info content.</param>
        public static (PlatformData Platform, ImageData Image)? GetMatchingPlatformData(PlatformInfo platform, RepoInfo repo, ImageArtifactDetails imageArtifactDetails)
        {
            RepoData repoData = imageArtifactDetails.Repos.FirstOrDefault(s => s.Repo == repo.Name);
            if (repoData == null || repoData.Images == null)
            {
                return null;
            }

            foreach (ImageData imageData in repoData.Images)
            {
                PlatformData platformData = imageData.Platforms
                    .FirstOrDefault(platformData => platformData.PlatformInfo == platform);
                if (platformData != null)
                {
                    return (platformData, imageData);
                }
            }

            return null;
        }

        private static bool ArePlatformsEqual(PlatformData platformData, ImageData imageData, PlatformInfo platform, ImageInfo manifestImage)
        {
            PlatformData otherPlatform = PlatformData.FromPlatformInfo(platform, manifestImage);
            // We can't use PlatformData.CompareTo here because it relies on having its PlatformInfo and ImageInfo values fully populated
            // which is what this class is trying to make happen.
            return !platformData.HasDifferentTagState(otherPlatform) &&
                platformData.GetIdentifier(excludeProductVersion: true) == otherPlatform.GetIdentifier(excludeProductVersion: true) &&
                AreProductVersionsEquivalent(imageData.ProductVersion, manifestImage.ProductVersion);
        }

        private static bool AreProductVersionsEquivalent(string productVersion1, string productVersion2)
        {
            if (productVersion1 == productVersion2)
            {
                return true;
            }

            // Product versions are considered equivalent if the major and minor segments are the same
            // See https://github.com/dotnet/docker-tools/issues/688
            return Version.TryParse(productVersion1, out Version version1) &&
                Version.TryParse(productVersion2, out Version version2) &&
                version1.ToString(2) == version2.ToString(2);
        }

    }
}
