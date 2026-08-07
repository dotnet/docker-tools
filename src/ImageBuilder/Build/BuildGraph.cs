// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// A current image definition that ImageBuilder can build.
/// </summary>
public sealed record BuildTarget(
    RepoInfo Repo,
    ImageInfo Image,
    PlatformInfo Platform,
    IReadOnlyDictionary<string, string> FromImageOverrides)
{
    public string DisplayName =>
        $"{Repo.Name} ({Platform.DockerfilePathRelativeToManifest})";
}

/// <summary>
/// Dependency, shared-build, and published-image data for build targets.
/// </summary>
public sealed record BuildGraph(
    IReadOnlyList<BuildTarget> Targets,
    IReadOnlyDictionary<BuildTarget, IReadOnlyList<BuildTarget>> Parents,
    IReadOnlyDictionary<BuildTarget, IReadOnlyList<BuildTarget>> Children,
    IReadOnlyDictionary<BuildTarget, IReadOnlyList<BuildTarget>> SharedBuildTargets)
{
    /// <summary>
    /// Creates a graph for every platform in the manifest.
    /// </summary>
    public static BuildGraph Create(ManifestInfo manifest) =>
        CreateForPlatforms(manifest, manifest.GetAllPlatforms());

    /// <summary>
    /// Creates a graph for the manifest platforms selected by its command-line filters.
    /// </summary>
    public static BuildGraph CreateFiltered(ManifestInfo manifest) =>
        CreateForPlatforms(manifest, manifest.GetFilteredPlatforms());

    /// <summary>
    /// Creates a graph for selected platforms after applying the manifest's command-line filters.
    /// </summary>
    public static BuildGraph CreateFiltered(
        ManifestInfo manifest,
        Func<PlatformInfo, bool> include) =>
        CreateForPlatforms(manifest, manifest.GetFilteredPlatforms().Where(include));

    private static BuildGraph CreateForPlatforms(
        ManifestInfo manifest,
        IEnumerable<PlatformInfo> platforms)
    {
        // Create build targets. These contain all the information needed to build an image.
        HashSet<PlatformInfo> platformSet = platforms.ToHashSet();
        BuildTarget[] targets = manifest.AllRepos
            .SelectMany(repo => repo.AllImages.SelectMany(image =>
                image.AllPlatforms
                    .Where(platformSet.Contains)
                    .Select(platform => new BuildTarget(
                        repo,
                        image,
                        platform,
                        GetFromImageOverrides(manifest, platform)))))
            .ToArray();

        // Index every platform and shared tag so internal FROM references can be resolved.
        Dictionary<string, List<BuildTarget>> targetsByTag = [];
        foreach (BuildTarget target in targets)
        {
            foreach (string tag in GetTags(target))
            {
                if (!targetsByTag.TryGetValue(tag, out List<BuildTarget>? taggedTargets))
                {
                    taggedTargets = [];
                    targetsByTag.Add(tag, taggedTargets);
                }

                taggedTargets.Add(target);
            }
        }

        // Create the dependency graph from internal FROM references.
        Dictionary<BuildTarget, IReadOnlyList<BuildTarget>> parents = targets.ToDictionary(
            target => target,
            target => (IReadOnlyList<BuildTarget>)target.Platform.InternalFromImages
                .Select(fromImage => ResolveParent(target, fromImage, targetsByTag))
                .Where(parent => parent is not null)
                .Cast<BuildTarget>()
                .Distinct()
                .ToArray());
        Dictionary<BuildTarget, List<BuildTarget>> mutableChildren = targets.ToDictionary(
            target => target,
            _ => new List<BuildTarget>());

        foreach ((BuildTarget child, IReadOnlyList<BuildTarget> targetParents) in parents)
        {
            foreach (BuildTarget parent in targetParents)
            {
                mutableChildren[parent].Add(child);
            }
        }

        Dictionary<BuildTarget, IReadOnlyList<BuildTarget>> children =
            mutableChildren.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<BuildTarget>)pair.Value);

        // Group targets that produce equivalent image content and can share published images.
        Dictionary<BuildTarget, IReadOnlyList<BuildTarget>> sharedBuildTargets = [];
        foreach (IGrouping<string, BuildTarget> group in targets.GroupBy(GetSharedBuildKey))
        {
            BuildTarget[] groupTargets = group.ToArray();
            foreach (BuildTarget target in groupTargets)
            {
                sharedBuildTargets[target] = groupTargets;
            }
        }

        return new BuildGraph(
            targets,
            parents,
            children,
            sharedBuildTargets);
    }

    private static IReadOnlyDictionary<string, string> GetFromImageOverrides(
        ManifestInfo manifest,
        PlatformInfo platform) =>
        platform.OverriddenFromImages
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                fromImage => fromImage,
                fromImage =>
                {
                    string fromRepo = DockerHelper.GetRepo(fromImage);
                    RepoInfo repo = manifest.AllRepos.First(repo =>
                        repo.FullModelName == fromRepo);
                    return DockerHelper.ReplaceRepo(fromImage, repo.QualifiedName);
                });

    private static IEnumerable<string> GetTags(BuildTarget target) =>
        target.Platform.Tags
            .Concat(target.Image.SharedTags)
            .Select(tag => tag.FullyQualifiedName)
            .Distinct(StringComparer.Ordinal);

    private static BuildTarget? ResolveParent(
        BuildTarget child,
        string fromImage,
        IReadOnlyDictionary<string, List<BuildTarget>> targetsByTag)
    {
        if (!targetsByTag.TryGetValue(fromImage, out List<BuildTarget>? candidates))
        {
            return null;
        }

        BuildTarget[] distinctCandidates = candidates.Distinct().ToArray();
        BuildTarget[] platformMatches = distinctCandidates
            .Where(candidate => HasSameTargetPlatform(candidate, child))
            .ToArray();
        BuildTarget[] matches = platformMatches.Length > 0
            ? platformMatches
            : distinctCandidates;

        return matches.Length switch
        {
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Internal image '{fromImage}' has {matches.Length} candidate platforms for " +
                $"'{child.Platform.DockerfilePathRelativeToManifest}'.")
        };
    }

    private static bool HasSameTargetPlatform(
        BuildTarget first,
        BuildTarget second) =>
        first.Platform.Model.OS == second.Platform.Model.OS &&
        first.Platform.Model.OsVersion == second.Platform.Model.OsVersion &&
        first.Platform.Model.Architecture == second.Platform.Model.Architecture &&
        first.Platform.Model.Variant == second.Platform.Model.Variant;

    private static string GetSharedBuildKey(BuildTarget target) =>
        JsonSerializer.Serialize(new
        {
            target.Platform.DockerfilePathRelativeToManifest,
            target.Platform.PlatformLabel,
            BuildArgs = target.Platform.BuildArgs
                .OrderBy(argument => argument.Key, StringComparer.Ordinal),
            FromImageOverrides = target.FromImageOverrides
                .OrderBy(image => image.Key, StringComparer.Ordinal)
        });
}
