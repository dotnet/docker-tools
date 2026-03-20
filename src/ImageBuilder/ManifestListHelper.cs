// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Describes a Docker manifest list to be created - a multi-arch tag
/// that references one or more platform-specific image tags.
/// </summary>
/// <param name="Tag">The fully-qualified manifest list tag (e.g., "mcr.microsoft.com/dotnet/aspnet:8.0").</param>
/// <param name="PlatformTags">The fully-qualified platform image tags included in this manifest list.</param>
public record ManifestListInfo(string Tag, IReadOnlyList<string> PlatformTags);

/// <summary>
/// Determines which Docker manifest lists should be created based on
/// the manifest definition and which platforms were actually built.
/// </summary>
public static class ManifestListHelper
{
    /// <summary>
    /// Returns the manifest lists that should be created for images that have
    /// shared tags and at least one platform present in
    /// <paramref name="imageArtifactDetails"/>. Only platforms present in
    /// <paramref name="imageArtifactDetails"/> are included in the results.
    /// </summary>
    public static IReadOnlyList<ManifestListInfo> GetManifestListsForImages(
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix)
    {
        IEnumerable<(RepoInfo Repo, ImageInfo Image)> imagesWithBuiltPlatforms = manifest.FilteredRepos
            .SelectMany(repo =>
                repo.FilteredImages
                    .Where(image => image.SharedTags.Any())
                    .Where(image => image.AllPlatforms
                        .Any(platform =>
                            ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails) != null))
                    .Select(image => (repo, image)))
            .ToList();

        return imagesWithBuiltPlatforms
            .SelectMany(pair => GetManifestListsForImage(pair.Repo, pair.Image, manifest, imageArtifactDetails, repoPrefix))
            .ToList()
            .AsReadOnly();
    }

    private static IEnumerable<ManifestListInfo> GetManifestListsForImage(
        RepoInfo repo,
        ImageInfo image,
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix)
    {
        // Manifest lists for normal (non-syndicated) shared tags
        IEnumerable<ManifestListInfo> primaryManifestLists = GetManifestListsForTags(
            repo, image, imageArtifactDetails,
            image.SharedTags.Select(tag => tag.Name),
            tag => DockerHelper.GetImageName(manifest.Registry, repoPrefix + repo.Name, tag),
            platform => platform.Tags.First());

        // Manifest lists for syndicated repos
        IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = image.SharedTags
            .Where(tag => tag.SyndicatedRepo != null)
            .GroupBy(tag => tag.SyndicatedRepo);

        IEnumerable<ManifestListInfo> syndicatedManifestLists = syndicatedTagGroups
            .SelectMany(syndicatedTags =>
            {
                string syndicatedRepo = syndicatedTags.Key;
                IEnumerable<string> destinationTags = syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags);

                return GetManifestListsForTags(
                    repo, image, imageArtifactDetails,
                    destinationTags,
                    tag => DockerHelper.GetImageName(manifest.Registry, repoPrefix + syndicatedRepo, tag),
                    platform => platform.Tags.FirstOrDefault(tag => tag.SyndicatedRepo == syndicatedRepo));
            });

        return primaryManifestLists.Concat(syndicatedManifestLists);
    }

    private static IEnumerable<ManifestListInfo> GetManifestListsForTags(
        RepoInfo repo,
        ImageInfo image,
        ImageArtifactDetails imageArtifactDetails,
        IEnumerable<string> tags,
        Func<string, string> getImageName,
        Func<PlatformInfo, TagInfo?> getTagRepresentative)
    {
        return tags
            .Select(tag => BuildManifestListInfo(repo, image, imageArtifactDetails, tag, getImageName, getTagRepresentative))
            .OfType<ManifestListInfo>();
    }

    private static ManifestListInfo? BuildManifestListInfo(
        RepoInfo repo,
        ImageInfo image,
        ImageArtifactDetails imageArtifactDetails,
        string tag,
        Func<string, string> getImageName,
        Func<PlatformInfo, TagInfo?> getTagRepresentative)
    {
        string manifestListTag = getImageName(tag);
        List<string> platformTags = [];

        foreach (PlatformInfo platform in image.AllPlatforms)
        {
            // Only include platforms that have entries in image-info (i.e., were actually built)
            (PlatformData Platform, ImageData Image)? platformMapping =
                ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails);

            if (platformMapping is null)
            {
                continue;
            }

            TagInfo? imageTag;
            if (platform.Tags.Any())
            {
                imageTag = getTagRepresentative(platform);
            }
            else
            {
                // Platform has no tags of its own - find a matching platform from another image
                PlatformInfo platformInfo = repo.AllImages
                    .SelectMany(img =>
                        img.AllPlatforms
                            .Select(p => (Image: img, Platform: p))
                            .Where(imagePlatform => platform != imagePlatform.Platform &&
                                PlatformInfo.AreMatchingPlatforms(image, platform, imagePlatform.Image, imagePlatform.Platform) &&
                                imagePlatform.Platform.Tags.Any()))
                    .FirstOrDefault()
                    .Platform;

                if (platformInfo is null)
                {
                    throw new InvalidOperationException(
                        $"Could not find a platform with concrete tags for '{platform.DockerfilePathRelativeToManifest}'.");
                }

                imageTag = getTagRepresentative(platformInfo);
            }

            if (imageTag is not null)
            {
                platformTags.Add(getImageName(imageTag.Name));
            }
        }

        if (platformTags.Count == 0)
        {
            return null;
        }

        return new ManifestListInfo(manifestListTag, platformTags.AsReadOnly());
    }
}
