// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Creates build plans for manifest platforms.
/// </summary>
public interface IBuildPlanner
{
    /// <summary>
    /// Evaluates the selected platforms and returns their build dispositions and causes.
    /// </summary>
    /// <param name="manifest">Manifest containing the platforms and their dependency graph.</param>
    /// <param name="platforms">Platforms whose state should be evaluated.</param>
    /// <param name="dependencyPlatforms">
    /// Platforms that may be included in dependency propagation and plan projections.
    /// </param>
    /// <param name="imageArtifactDetails">Previously published image metadata, when available.</param>
    /// <param name="baseImageResolver">Resolver for current base-image identities.</param>
    /// <param name="sourceRepoUrl">
    /// Source repository URL used to compare Dockerfile commits. When null, the Dockerfile
    /// comparison is not performed.
    /// </param>
    /// <param name="checks">Checks to evaluate for each platform.</param>
    Task<BuildPlan> CreateBuildPlanAsync(
        ManifestInfo manifest,
        IEnumerable<PlatformInfo> platforms,
        IEnumerable<PlatformInfo> dependencyPlatforms,
        ImageArtifactDetails? imageArtifactDetails,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        IReadOnlyList<IBuildPlanCheck> checks);

}

/// <inheritdoc/>
public class BuildPlanner(ILogger<BuildPlanner> logger, IGitService gitService) : IBuildPlanner
{
    /// <inheritdoc/>
    public async Task<BuildPlan> CreateBuildPlanAsync(
        ManifestInfo manifest,
        IEnumerable<PlatformInfo> platforms,
        IEnumerable<PlatformInfo> dependencyPlatforms,
        ImageArtifactDetails? imageArtifactDetails,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        IReadOnlyList<IBuildPlanCheck> checks)
    {
        HashSet<PlatformInfo> evaluatedPlatforms = platforms.ToHashSet();
        PlatformInfo[] plannedPlatforms = dependencyPlatforms.Distinct().ToArray();
        List<BuildPlanEntry> entries = [];
        Dictionary<PlatformInfo, (PlatformData Platform, ImageData Image)?> matchingPlatforms = [];

        foreach (PlatformInfo platform in plannedPlatforms)
        {
            if (!evaluatedPlatforms.Contains(platform))
            {
                entries.Add(new BuildPlanEntry(platform, BuildDisposition.Skip, null, []));
                continue;
            }

            RepoInfo repo = manifest.GetRepoByImage(manifest.GetImageByPlatform(platform));
            (PlatformData Platform, ImageData Image)? matchingPlatform =
                imageArtifactDetails is null ?
                    null :
                    ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails);
            matchingPlatforms[platform] = matchingPlatform;

            BuildPlanCheckContext context = new(
                platform,
                matchingPlatform?.Platform,
                baseImageResolver,
                gitService,
                logger,
                sourceRepoUrl);
            IReadOnlyList<EvaluatedBuildPlanCheck> results =
                await EvaluateChecksAsync(checks, context);
            BuildPlanEntry entry = CreateEntry(platform, matchingPlatform?.Platform, results);
            entries.Add(entry);

            if (entry.CachedPlatform is not null)
            {
                RecordAvailableImage(
                    manifest,
                    platform,
                    entry.CachedPlatform,
                    baseImageResolver);
            }
        }

        entries = (await ReuseEquivalentBuildsAsync(
            manifest,
            entries,
            matchingPlatforms,
            baseImageResolver,
            sourceRepoUrl,
            checks))
            .ToList();

        return new BuildPlan(manifest, PropagateBuildCauses(manifest, entries, plannedPlatforms));
    }

    private static BuildPlanEntry CreateEntry(
        PlatformInfo platform,
        PlatformData? cachedPlatform,
        IReadOnlyCollection<EvaluatedBuildPlanCheck> results)
    {
        BuildDisposition disposition = results.Any(result =>
            result.Disposition == BuildPlanCheckDisposition.Build) ?
            BuildDisposition.Build :
            results.Any(result =>
                result.Disposition == BuildPlanCheckDisposition.ReuseAndPublish) ?
                BuildDisposition.ReuseAndPublish :
                BuildDisposition.Reuse;

        if (disposition is BuildDisposition.Reuse or BuildDisposition.ReuseAndPublish &&
            cachedPlatform is null)
        {
            throw new InvalidOperationException(
                $"Planning checks produced '{disposition}' for " +
                $"'{platform.DockerfilePathRelativeToManifest}' without cached image metadata.");
        }

        return new BuildPlanEntry(
            platform,
            disposition,
            disposition is BuildDisposition.Build ? null : cachedPlatform,
            results
                .Select(result => CreateDirectCause(platform, result.Reason))
                .ToArray());
    }

    private static BuildCause CreateDirectCause(PlatformInfo platform, BuildPlanReason reason) =>
        new(reason, platform, [platform]);

    private static async Task<IReadOnlyList<EvaluatedBuildPlanCheck>> EvaluateChecksAsync(
        IEnumerable<IBuildPlanCheck> checks,
        BuildPlanCheckContext context)
    {
        List<EvaluatedBuildPlanCheck> results = [];
        foreach (IBuildPlanCheck check in checks)
        {
            BuildPlanCheckDisposition? disposition = await check.EvaluateAsync(context);
            if (disposition is not null)
            {
                results.Add(new EvaluatedBuildPlanCheck(check.Reason, disposition.Value));
            }
        }

        return results;
    }

    private sealed record EvaluatedBuildPlanCheck(
        BuildPlanReason Reason,
        BuildPlanCheckDisposition Disposition);

    private static void RecordAvailableImage(
        ManifestInfo manifest,
        PlatformInfo platform,
        PlatformData cachedPlatform,
        BaseImageResolver baseImageResolver)
    {
        ImageInfo image = manifest.GetImageByPlatform(platform);
        foreach (TagInfo tag in platform.Tags.Concat(image.SharedTags))
        {
            baseImageResolver.RecordPlannedAvailableImage(tag.FullyQualifiedName, cachedPlatform.Digest);
        }
    }

    private async Task<IReadOnlyList<BuildPlanEntry>> ReuseEquivalentBuildsAsync(
        ManifestInfo manifest,
        IReadOnlyCollection<BuildPlanEntry> entries,
        IReadOnlyDictionary<PlatformInfo, (PlatformData Platform, ImageData Image)?> matchingPlatforms,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        IReadOnlyList<IBuildPlanCheck> checks)
    {
        Dictionary<PlatformInfo, BuildPlanEntry> entriesByPlatform =
            entries.ToDictionary(entry => entry.Platform);

        IGrouping<string, BuildPlanEntry>[] sharedBuildGroups = entriesByPlatform.Values
            .Where(entry => entry.Disposition != BuildDisposition.Skip)
            .GroupBy(entry => GetBuildCacheKey(entry.Platform))
            .ToArray();
        foreach (IGrouping<string, BuildPlanEntry> group in sharedBuildGroups)
        {
            ApplyEquivalentBuildReuse(
                manifest,
                group,
                matchingPlatforms,
                baseImageResolver,
                entriesByPlatform);
        }

        // ponytail: repeated graph walks keep this simple; topologically sort once if manifests grow large.
        IEnumerable<PlatformInfo> dependencyOrderedPlatforms = entries
            .Select(entry => entry.Platform)
            .OrderBy(platform => manifest
                .GetAncestors(platform, entriesByPlatform.Keys)
                .Distinct()
                .Count());

        foreach (PlatformInfo platform in dependencyOrderedPlatforms)
        {
            BuildPlanEntry entry = entriesByPlatform[platform];
            if (entry.Disposition != BuildDisposition.Build ||
                !entry.Causes.Any(cause => cause.Reason == BuildPlanReason.BaseImageChanged) ||
                !matchingPlatforms.TryGetValue(
                    platform,
                    out (PlatformData Platform, ImageData Image)? matchingPlatform) ||
                matchingPlatform is null)
            {
                continue;
            }

            BuildPlanCheckContext context = new(
                platform,
                matchingPlatform.Value.Platform,
                baseImageResolver,
                gitService,
                logger,
                sourceRepoUrl);
            IReadOnlyList<EvaluatedBuildPlanCheck> reevaluatedResults =
                await EvaluateChecksAsync(checks, context);
            BuildPlanEntry reevaluatedEntry = CreateEntry(
                platform,
                matchingPlatform.Value.Platform,
                reevaluatedResults);
            if (AreEquivalent(entry, reevaluatedEntry))
            {
                continue;
            }

            entriesByPlatform[platform] = reevaluatedEntry;
            if (reevaluatedEntry.CachedPlatform is null)
            {
                continue;
            }

            RecordAvailableImage(
                manifest,
                platform,
                reevaluatedEntry.CachedPlatform,
                baseImageResolver);

            BuildPlanEntry[] equivalentEntries = entriesByPlatform.Values
                .Where(entry =>
                    entry.Disposition != BuildDisposition.Skip &&
                    GetBuildCacheKey(entry.Platform) == GetBuildCacheKey(platform))
                .ToArray();
            ApplyEquivalentBuildReuse(
                manifest,
                equivalentEntries,
                matchingPlatforms,
                baseImageResolver,
                entriesByPlatform);
        }

        return entries
            .Select(entry => entriesByPlatform[entry.Platform])
            .ToArray();
    }

    private static void ApplyEquivalentBuildReuse(
        ManifestInfo manifest,
        IEnumerable<BuildPlanEntry> entries,
        IReadOnlyDictionary<PlatformInfo, (PlatformData Platform, ImageData Image)?> matchingPlatforms,
        BaseImageResolver baseImageResolver,
        IDictionary<PlatformInfo, BuildPlanEntry> entriesByPlatform)
    {
        BuildPlanEntry[] equivalentEntries = entries.ToArray();
        BuildPlanEntry? reusableEntry = equivalentEntries.FirstOrDefault(entry =>
            entry.Disposition is BuildDisposition.Reuse or BuildDisposition.ReuseAndPublish &&
            entry.CachedPlatform is not null);

        if (reusableEntry?.CachedPlatform is not PlatformData sharedCachedPlatform)
        {
            return;
        }

        foreach (BuildPlanEntry entry in equivalentEntries)
        {
            matchingPlatforms.TryGetValue(
                entry.Platform,
                out (PlatformData Platform, ImageData Image)? matchingPlatform);
            PlatformData? aliasPlatform = matchingPlatform?.Platform;
            List<EvaluatedBuildPlanCheck> sharedReuseResults = [];
            if (aliasPlatform is null || !HasAllTagsPublished(aliasPlatform))
            {
                sharedReuseResults.Add(new EvaluatedBuildPlanCheck(
                    BuildPlanReason.MissingTags,
                    BuildPlanCheckDisposition.ReuseAndPublish));
            }

            if (aliasPlatform is not null &&
                !DockerHelper.GetDigestSha(aliasPlatform.Digest).Equals(
                    DockerHelper.GetDigestSha(sharedCachedPlatform.Digest),
                    StringComparison.OrdinalIgnoreCase))
            {
                sharedReuseResults.Add(new EvaluatedBuildPlanCheck(
                    BuildPlanReason.EquivalentBuildChanged,
                    BuildPlanCheckDisposition.ReuseAndPublish));
            }

            entriesByPlatform[entry.Platform] =
                CreateEntry(entry.Platform, sharedCachedPlatform, sharedReuseResults);
            RecordAvailableImage(
                manifest,
                entry.Platform,
                sharedCachedPlatform,
                baseImageResolver);
        }
    }

    private static IReadOnlyList<BuildPlanEntry> PropagateBuildCauses(
        ManifestInfo manifest,
        IEnumerable<BuildPlanEntry> entries,
        IReadOnlyCollection<PlatformInfo> plannedPlatforms)
    {
        Dictionary<PlatformInfo, BuildPlanEntry> entriesByPlatform =
            entries.ToDictionary(entry => entry.Platform);
        BuildPlanEntry[] directlyBuiltEntries = entriesByPlatform.Values
            .Where(entry => entry.Disposition == BuildDisposition.Build)
            .ToArray();

        foreach (BuildPlanEntry directEntry in directlyBuiltEntries)
        {
            Queue<(PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path)> queue = new();
            queue.Enqueue((directEntry.Platform, [directEntry.Platform]));
            HashSet<PlatformInfo> visited = [directEntry.Platform];

            while (queue.TryDequeue(out (PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path) current))
            {
                IEnumerable<PlatformInfo> children = plannedPlatforms.Where(candidate =>
                    manifest.GetParents(candidate, plannedPlatforms).Contains(current.Platform));

                foreach (PlatformInfo child in children.Where(visited.Add))
                {
                    PlatformInfo[] childPath = [..current.Path, child];
                    BuildPlanEntry childEntry = entriesByPlatform[child];
                    BuildCause[] propagatedCauses = directEntry.Causes
                        .Select(cause => cause with { DependencyPath = childPath })
                        .ToArray();

                    entriesByPlatform[child] = childEntry with
                    {
                        Disposition = BuildDisposition.Build,
                        CachedPlatform = null,
                        Causes = childEntry.Causes.Concat(propagatedCauses).Distinct().ToArray()
                    };

                    queue.Enqueue((child, childPath));
                }
            }
        }

        return entries
            .Select(entry => entriesByPlatform[entry.Platform])
            .ToArray();
    }

    private static bool AreEquivalent(BuildPlanEntry left, BuildPlanEntry right) =>
        left.Disposition == right.Disposition &&
        left.CachedPlatform == right.CachedPlatform &&
        left.Causes.Select(cause => (cause.Reason, cause.Origin))
            .SequenceEqual(right.Causes.Select(cause => (cause.Reason, cause.Origin)));

    private static bool HasAllTagsPublished(PlatformData platform) =>
        (platform.PlatformInfo?.Tags ?? [])
            .Select(tag => tag.Name)
            .AreEquivalent(platform.SimpleTags);

    /// <summary>
    /// Builds a cache key that uniquely identifies a platform build based on its Dockerfile path
    /// and build arguments.
    /// </summary>
    private static string GetBuildCacheKey(PlatformInfo platform) =>
        $"{platform.DockerfilePathRelativeToManifest}-" +
        string.Join('-', platform.BuildArgs
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));

}
