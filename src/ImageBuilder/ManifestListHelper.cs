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
/// Describes platforms that are expected by the manifest but missing from a generated manifest list.
/// </summary>
/// <param name="ManifestListTag">The fully-qualified manifest list tag.</param>
/// <param name="MissingPlatforms">Descriptions of the expected platforms missing from the tag.</param>
public record ManifestListPlatformValidationIssue(string ManifestListTag, IReadOnlyList<string> MissingPlatforms);

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
        IEnumerable<(RepoInfo Repo, ImageInfo Image)> imagesWithBuiltPlatforms =
            GetImagesWithBuiltPlatforms(manifest, imageArtifactDetails);

        return imagesWithBuiltPlatforms
            .SelectMany(pair => GetManifestListsForImage(pair.Repo, pair.Image, manifest, imageArtifactDetails, repoPrefix))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Validates that each generated manifest list contains every platform expected by the manifest.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when one or more generated manifest list tags would omit expected platforms.
    /// </exception>
    public static void ValidateManifestListPlatforms(
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix)
    {
        IReadOnlyList<ManifestListPlatformValidationIssue> issues = GetManifestListPlatformValidationIssues(
            manifest, imageArtifactDetails, repoPrefix);

        if (issues.Count == 0)
        {
            return;
        }

        string details = string.Join(
            Environment.NewLine,
            issues.Select(issue =>
                $"- {issue.ManifestListTag}: {string.Join(", ", issue.MissingPlatforms)}"));

        throw new InvalidOperationException(
            $"Generated manifest list tags are missing expected platforms defined in the manifest:{Environment.NewLine}{details}");
    }

    /// <summary>
    /// Gets validation issues for generated manifest lists that would omit expected platforms.
    /// </summary>
    public static IReadOnlyList<ManifestListPlatformValidationIssue> GetManifestListPlatformValidationIssues(
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix)
    {
        IEnumerable<(RepoInfo Repo, ImageInfo Image)> imagesWithBuiltPlatforms =
            GetImagesWithBuiltPlatforms(manifest, imageArtifactDetails);

        return imagesWithBuiltPlatforms
            .SelectMany(pair => GetManifestListPlatformValidationIssuesForImage(
                pair.Repo, pair.Image, manifest, imageArtifactDetails, repoPrefix))
            .ToList()
            .AsReadOnly();
    }

    private static IEnumerable<(RepoInfo Repo, ImageInfo Image)> GetImagesWithBuiltPlatforms(
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails) =>
        manifest.FilteredRepos
            .SelectMany(repo =>
                repo.FilteredImages
                    .Where(image => image.SharedTags.Any())
                    .Where(image => image.AllPlatforms
                        .Any(platform =>
                            ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails) != null))
                    .Select(image => (repo, image)))
            .ToList();

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

    private static IEnumerable<ManifestListPlatformValidationIssue> GetManifestListPlatformValidationIssuesForImage(
        RepoInfo repo,
        ImageInfo image,
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix)
    {
        IEnumerable<ManifestListPlatformValidationIssue> primaryManifestListIssues = GetManifestListPlatformValidationIssuesForTags(
            repo, image, imageArtifactDetails,
            image.SharedTags.Select(tag => tag.Name),
            tag => DockerHelper.GetImageName(manifest.Registry, repoPrefix + repo.Name, tag),
            platform => platform.Tags.First());

        IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = image.SharedTags
            .Where(tag => tag.SyndicatedRepo != null)
            .GroupBy(tag => tag.SyndicatedRepo);

        IEnumerable<ManifestListPlatformValidationIssue> syndicatedManifestListIssues = syndicatedTagGroups
            .SelectMany(syndicatedTags =>
            {
                string syndicatedRepo = syndicatedTags.Key;
                IEnumerable<string> destinationTags = syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags);

                return GetManifestListPlatformValidationIssuesForTags(
                    repo, image, imageArtifactDetails,
                    destinationTags,
                    tag => DockerHelper.GetImageName(manifest.Registry, repoPrefix + syndicatedRepo, tag),
                    platform => platform.Tags.FirstOrDefault(tag => tag.SyndicatedRepo == syndicatedRepo));
            });

        return primaryManifestListIssues.Concat(syndicatedManifestListIssues);
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

    private static IEnumerable<ManifestListPlatformValidationIssue> GetManifestListPlatformValidationIssuesForTags(
        RepoInfo repo,
        ImageInfo image,
        ImageArtifactDetails imageArtifactDetails,
        IEnumerable<string> tags,
        Func<string, string> getImageName,
        Func<PlatformInfo, TagInfo?> getTagRepresentative)
    {
        return tags
            .Select(tag => BuildManifestListPlatformValidationIssue(
                repo, image, imageArtifactDetails, tag, getImageName, getTagRepresentative))
            .OfType<ManifestListPlatformValidationIssue>();
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
            imageTag = GetPlatformTagRepresentative(repo, image, platform, getTagRepresentative, throwIfMissing: true);

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

    private static ManifestListPlatformValidationIssue? BuildManifestListPlatformValidationIssue(
        RepoInfo repo,
        ImageInfo image,
        ImageArtifactDetails imageArtifactDetails,
        string tag,
        Func<string, string> getImageName,
        Func<PlatformInfo, TagInfo?> getTagRepresentative)
    {
        string manifestListTag = getImageName(tag);
        List<string> missingPlatforms = [];
        bool hasExpectedPlatform = false;

        foreach (PlatformInfo platform in image.AllPlatforms)
        {
            TagInfo? imageTag = GetPlatformTagRepresentative(repo, image, platform, getTagRepresentative, throwIfMissing: false);
            if (imageTag is null)
            {
                continue;
            }

            hasExpectedPlatform = true;

            if (ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails) is null)
            {
                missingPlatforms.Add(GetPlatformDescription(platform));
            }
        }

        if (!hasExpectedPlatform || missingPlatforms.Count == 0)
        {
            return null;
        }

        return new ManifestListPlatformValidationIssue(manifestListTag, missingPlatforms.AsReadOnly());
    }

    private static TagInfo? GetPlatformTagRepresentative(
        RepoInfo repo,
        ImageInfo image,
        PlatformInfo platform,
        Func<PlatformInfo, TagInfo?> getTagRepresentative,
        bool throwIfMissing)
    {
        if (platform.Tags.Any())
        {
            return getTagRepresentative(platform);
        }

        // Tagless platforms included by shared tags need a matching concrete tag to reference in the manifest list.
        (ImageInfo Image, PlatformInfo Platform)? matchingPlatform =
            repo.AllImages
                .SelectMany(candidateImage =>
                    candidateImage.AllPlatforms
                        .Select(candidatePlatform => (Image: candidateImage, Platform: candidatePlatform))
                        .Where(candidate =>
                            platform != candidate.Platform
                            && PlatformInfo.AreMatchingPlatforms(
                                image1: image,
                                platform1: platform,
                                image2: candidate.Image,
                                platform2: candidate.Platform)
                            && candidate.Platform.Tags.Any()))
                .Cast<(ImageInfo Image, PlatformInfo Platform)?>()
                .FirstOrDefault();

        if (matchingPlatform is not null)
        {
            return getTagRepresentative(matchingPlatform.Value.Platform);
        }

        if (throwIfMissing)
        {
            throw new InvalidOperationException(
                $"Could not find a platform with concrete tags for '{platform.DockerfilePathRelativeToManifest}'.");
        }

        return null;
    }

    private static string GetPlatformDescription(PlatformInfo platform) =>
        $"{platform.PlatformLabel} ({platform.DockerfilePathRelativeToManifest})";
}
