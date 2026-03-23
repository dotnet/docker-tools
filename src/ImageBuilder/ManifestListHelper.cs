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
                // This platform has no tags of its own (it's a "tagless" platform included
                // only via shared tags). To reference it in the manifest list, we need a tag.
                // Search all images in the repo for a different platform that builds the same
                // Dockerfile/OS/arch and does have tags, then borrow one of its tags.
                (ImageInfo Image, PlatformInfo Platform)? matchingPlatform =
                    repo.AllImages
                    .SelectMany(candidateImage =>
                        candidateImage.AllPlatforms
                        .Select(candidatePlatform => (Image: candidateImage, Platform: candidatePlatform))
                        .Where(candidate =>
                            // Exclude the current platform itself
                            platform != candidate.Platform
                            // Must be the same Dockerfile, OS, and architecture
                            && PlatformInfo.AreMatchingPlatforms(
                                image1: candidateImage,
                                platform1: platform,
                                image2: candidate.Image,
                                platform2: candidate.Platform)
                            // Must actually have tags we can borrow
                            && candidate.Platform.Tags.Any()
                        )
                    )
                    // Cast to nullable so FirstOrDefault returns null (not a default struct)
                    // when no match is found, allowing the ?? to throw.
                    .Cast<(ImageInfo Image, PlatformInfo Platform)?>()
                    .FirstOrDefault()
                        ?? throw new InvalidOperationException(
                            $"Could not find a platform with concrete tags for"
                            + $" '{platform.DockerfilePathRelativeToManifest}'.");

                imageTag = getTagRepresentative(matchingPlatform.Value.Platform);
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
