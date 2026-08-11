// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder;

public static class ImageInfoMergeExtensions
{
    /// <summary>
    /// Merges source image artifact data into an existing target.
    /// </summary>
    /// <remarks>
    /// Existing repos, images, and platforms are matched by their model comparison rules.
    /// Source scalar values replace target values, while tag collections are either merged or
    /// replaced according to <see cref="ImageInfoMergeOptions.IsPublish"/>.
    /// </remarks>
    public static void MergeInto(
        this ImageArtifactDetails source,
        ImageArtifactDetails target,
        ImageInfoMergeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        options ??= new ImageInfoMergeOptions();
        target.Repos = MergeLists(
            source.Repos,
            target.Repos,
            (sourceRepo, targetRepo) => sourceRepo.MergeInto(targetRepo, options));
    }

    private static void MergeInto(this RepoData source, RepoData target, ImageInfoMergeOptions options)
    {
        target.Repo = source.Repo;
        target.Images = MergeLists(
            source.Images,
            target.Images,
            (sourceImage, targetImage) => sourceImage.MergeInto(targetImage, options));
    }

    private static void MergeInto(this ImageData source, ImageData target, ImageInfoMergeOptions options)
    {
        target.ProductVersion = source.ProductVersion;

        if (source.Manifest is null)
        {
            target.Manifest = null;
        }
        else if (target.Manifest is null)
        {
            target.Manifest = source.Manifest;
        }
        else
        {
            source.Manifest.MergeInto(target.Manifest, options);
        }

        target.Platforms = MergeLists(
            source.Platforms,
            target.Platforms,
            (sourcePlatform, targetPlatform) => sourcePlatform.MergeInto(targetPlatform, options));
    }

    private static void MergeInto(this PlatformData source, PlatformData target, ImageInfoMergeOptions options)
    {
        target.Dockerfile = source.Dockerfile;
        target.SimpleTags = MergeStringLists(source.SimpleTags, target.SimpleTags, replace: options.IsPublish);
        target.Digest = source.Digest;
        target.BaseImageDigest = source.BaseImageDigest;
        target.OsType = source.OsType;
        target.OsVersion = source.OsVersion;
        target.Architecture = source.Architecture;
        target.Created = source.Created;
        target.CommitUrl = source.CommitUrl;

        // Layers are always unique and should never get merged.
        // Layers are already sorted according to their position in the image. They should not be sorted alphabetically.
        target.Layers = source.Layers;
        target.IsUnchanged = source.IsUnchanged;
    }

    private static void MergeInto(this ManifestData source, ManifestData target, ImageInfoMergeOptions options)
    {
        target.Digest = source.Digest;

        target.SyndicatedDigests = MergeNullableStringLists(
            source.SyndicatedDigests,
            target.SyndicatedDigests,
            replace: options.IsPublish);

        target.Created = source.Created;

        target.SharedTags = MergeNullableStringLists(source.SharedTags, target.SharedTags, replace: options.IsPublish);
    }

    private static List<T> MergeLists<T>(List<T> source, List<T> target, Action<T, T> merge)
        where T : class, IComparable<T>
    {
        if (source is null || source.Count == 0)
        {
            return target;
        }

        if (target?.Count > 0)
        {
            foreach (T sourceItem in source)
            {
                T? matchingTargetItem = target.FirstOrDefault(targetItem => sourceItem.CompareTo(targetItem) == 0);

                if (matchingTargetItem is null)
                {
                    target.Add(sourceItem);
                }
                else
                {
                    merge(sourceItem, matchingTargetItem);
                }
            }
        }
        else
        {
            target = source;
        }

        List<T> sortedList = target.ToList();
        sortedList.Sort();
        return sortedList;
    }

    private static List<string> MergeStringLists(List<string> source, List<string> target, bool replace) =>
        MergeNullableStringLists(source, target, replace)
        ?? throw new InvalidOperationException("Image platform tag lists cannot be null.");

    private static List<string>? MergeNullableStringLists(List<string>? source, List<string>? target, bool replace)
    {
        if (replace)
        {
            // Tags can be merged or replaced depending on the scenario.
            // When merging multiple image info files together into a single file, the tags should be
            // merged to account for cases where tags for a given image are spread across multiple
            // image info files. But when publishing an image info file the source tags should replace
            // the destination tags. Any of the image's tags contained in the target should be considered
            // obsolete and should be replaced by the source. This accounts for the scenario where shared
            // tags are moved from one image to another. If we had merged instead of replaced, then the
            // shared tag would not have been removed from the original image in the image info in such
            // a scenario.
            // See:
            // - https://github.com/dotnet/docker-tools/pull/269
            // - https://github.com/dotnet/docker-tools/issues/289
            return source?.OrderBy(item => item).ToList();
        }

        if (source is null || source.Count == 0)
        {
            return target;
        }

        return target is null ? source : target.Union(source).OrderBy(item => item).ToList();
    }
}
