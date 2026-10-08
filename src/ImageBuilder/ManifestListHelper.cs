// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        string qualifiedRepo = DockerHelper.GetImageName(manifest.Registry, repoPrefix + repo.Name);
        return image.SharedTags
            .Select(tag => BuildManifestListInfo(repo, image, imageArtifactDetails, tag.Name, qualifiedRepo))
            .OfType<ManifestListInfo>();
    }

    private static IEnumerable<ManifestListPlatformValidationIssue> GetManifestListPlatformValidationIssuesForImage(
        RepoInfo repo,
        ImageInfo image,
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix)
    {
        string qualifiedRepo = DockerHelper.GetImageName(manifest.Registry, repoPrefix + repo.Name);
        return image.SharedTags
            .Select(tag =>
                BuildManifestListPlatformValidationIssue(repo, image, imageArtifactDetails, tag.Name, qualifiedRepo))
            .OfType<ManifestListPlatformValidationIssue>();
    }

    private static ManifestListInfo? BuildManifestListInfo(
        RepoInfo repo,
        ImageInfo image,
        ImageArtifactDetails imageArtifactDetails,
        string tag,
        string qualifiedRepo)
    {
        string manifestListTag = TagInfo.GetFullyQualifiedName(qualifiedRepo, tag);
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

            // TODO: support platforms without tags (https://github.com/dotnet/docker-tools/issues/1499).
            if (TryGetPlatformTagRepresentative(repo, image, platform, out TagInfo? imageTag))
            {
                platformTags.Add(TagInfo.GetFullyQualifiedName(qualifiedRepo, imageTag.Name));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Could not find a platform with concrete tags for '{platform.DockerfilePathRelativeToManifest}'.");
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
        ImageInfo manifestImage,
        ImageArtifactDetails imageArtifactDetails,
        string tag,
        string qualifiedRepo)
    {
        string manifestListTag = TagInfo.GetFullyQualifiedName(qualifiedRepo, tag);
        List<string> missingPlatforms = [];
        bool hasExpectedPlatform = false;

        // For each platform declared in the manifest, make sure the image-info (imageArtifactDetails) also contains
        // that platform. If not, there's a problem.
        foreach (PlatformInfo platform in manifestImage.AllPlatforms)
        {
            if (!TryGetPlatformTagRepresentative(repo, manifestImage, platform, out _))
                continue;

            hasExpectedPlatform = true;

            if (ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails) is null)
                missingPlatforms.Add(GetPlatformDescription(platform));
        }

        if (!hasExpectedPlatform || missingPlatforms.Count == 0)
            return null;

        return new ManifestListPlatformValidationIssue(manifestListTag, missingPlatforms.AsReadOnly());
    }

    /// <summary>
    /// Resolves the concrete tag used to reference <paramref name="platform"/> in a manifest list,
    /// borrowing from a matching sibling platform when the platform is tagless.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> and a non-null <paramref name="representativeTag"/> when a usable tag
    /// is found; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryGetPlatformTagRepresentative(
        RepoInfo repo,
        ImageInfo image,
        PlatformInfo platform,
        [NotNullWhen(true)] out TagInfo? representativeTag)
    {
        if (platform.Tags.Any())
        {
            representativeTag = platform.Tags.First();
            return true;
        }

        // Tagless platforms included by shared tags need a matching concrete tag to reference in
        // the manifest list, borrowed from a sibling platform (same Dockerfile/OS/arch).
        representativeTag = repo.AllImages
            .SelectMany(candidateImage =>
                candidateImage.AllPlatforms
                    .Select(candidatePlatform => (Image: candidateImage, Platform: candidatePlatform)))
            .Where(candidate =>
                platform != candidate.Platform
                && PlatformInfo.AreMatchingPlatforms(
                    image1: image,
                    platform1: platform,
                    image2: candidate.Image,
                    platform2: candidate.Platform)
                && candidate.Platform.Tags.Any())
            .Select(candidate => candidate.Platform.Tags.First())
            .FirstOrDefault();

        return representativeTag is not null;
    }

    private static string GetPlatformDescription(PlatformInfo platform) =>
        $"{platform.PlatformLabel} ({platform.DockerfilePathRelativeToManifest})";
}
