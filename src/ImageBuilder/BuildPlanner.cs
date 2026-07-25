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
        Dictionary<PlatformInfo, BuildPlanEntry> entriesByPlatform = [];

        foreach (PlatformInfo platform in
            plannedPlatforms.Where(platform => !evaluatedPlatforms.Contains(platform)))
        {
            entriesByPlatform[platform] = new BuildPlanEntry(platform, BuildDisposition.Skip, null, []);
        }

        PlatformInfo[] platformsToEvaluate =
            [..plannedPlatforms.Where(evaluatedPlatforms.Contains)];
        Dictionary<PlatformInfo, PlatformData?> previousPlatforms = platformsToEvaluate.ToDictionary(
            platform => platform,
            platform => GetPreviousPlatform(manifest, platform, imageArtifactDetails));

        IBuildPlanCheck[] contentChecks = [..checks.Where(IsContentCheck)];
        IBuildPlanCheck[] publishChecks = [..checks.Where(check => !IsContentCheck(check))];

        // Platforms that share a Dockerfile and build args produce the same image content, so the
        // content checks are evaluated once per group. Groups are evaluated in dependency order so
        // that a parent's reuse decision is recorded before its children resolve their base image.
        IEnumerable<IGrouping<string, PlatformInfo>> platformGroups = platformsToEvaluate
            .GroupBy(GetBuildCacheKey)
            .OrderBy(group => group.Max(platform => manifest
                .GetAncestors(platform, plannedPlatforms)
                .Distinct()
                .Count()));

        foreach (IGrouping<string, PlatformInfo> group in platformGroups)
        {
            PlatformInfo representative = SelectContentRepresentative(group, previousPlatforms);
            PlatformData? reusedPlatform = previousPlatforms[representative];
            IReadOnlyList<EvaluatedBuildPlanCheck> contentResults = await EvaluateChecksAsync(
                contentChecks,
                CreateContext(representative, reusedPlatform));
            bool requiresBuild = contentResults.Any(result =>
                result.Disposition == BuildPlanCheckDisposition.Build);

            foreach (PlatformInfo platform in group)
            {
                PlatformData? previousPlatform = previousPlatforms[platform];
                List<EvaluatedBuildPlanCheck> results = [..contentResults];
                results.AddRange(
                    await EvaluateChecksAsync(publishChecks, CreateContext(platform, previousPlatform)));

                if (!requiresBuild && HasEquivalentBuildChanged(previousPlatform, reusedPlatform))
                {
                    results.Add(new EvaluatedBuildPlanCheck(
                        BuildPlanReason.EquivalentBuildChanged,
                        BuildPlanCheckDisposition.ReuseAndPublish));
                }

                BuildPlanEntry entry = CreateEntry(platform, reusedPlatform, results);
                entriesByPlatform[platform] = entry;

                if (entry.CachedPlatform is not null)
                {
                    RecordAvailableImage(
                        manifest,
                        platform,
                        entry.CachedPlatform,
                        baseImageResolver);
                }
            }
        }

        BuildPlanEntry[] entries = [..plannedPlatforms.Select(platform => entriesByPlatform[platform])];
        return new BuildPlan(manifest, PropagateBuildCauses(manifest, entries, plannedPlatforms));

        BuildPlanCheckContext CreateContext(PlatformInfo platform, PlatformData? previousPlatform) =>
            new(platform, previousPlatform, baseImageResolver, gitService, logger, sourceRepoUrl);
    }

    private static PlatformData? GetPreviousPlatform(
        ManifestInfo manifest,
        PlatformInfo platform,
        ImageArtifactDetails? imageArtifactDetails)
    {
        if (imageArtifactDetails is null)
        {
            return null;
        }

        RepoInfo repo = manifest.GetRepoByImage(manifest.GetImageByPlatform(platform));
        return ImageInfoHelper
            .GetMatchingPlatformData(platform, repo, imageArtifactDetails)?.Platform;
    }

    /// <summary>
    /// Indicates whether a check answers whether the image content is still valid. Content depends
    /// only on the Dockerfile and its base image; the remaining checks answer whether an individual
    /// platform's copy of that content is published correctly.
    /// </summary>
    private static bool IsContentCheck(IBuildPlanCheck check) =>
        check.Reason is not BuildPlanReason.MissingTags;

    /// <summary>
    /// Selects the platform whose previously published metadata represents the content shared by
    /// the group. Platforms with previously published metadata are preferred because the content
    /// checks need it to have an opinion.
    /// </summary>
    private static PlatformInfo SelectContentRepresentative(
        IEnumerable<PlatformInfo> group,
        IReadOnlyDictionary<PlatformInfo, PlatformData?> previousPlatforms) =>
        group
            .OrderByDescending(platform => previousPlatforms[platform] is not null)
            .ThenBy(platform => platform.DockerfilePathRelativeToManifest, StringComparer.Ordinal)
            .ThenBy(platform => platform.Tags.FirstOrDefault()?.Name, StringComparer.Ordinal)
            .First();

    /// <summary>
    /// Indicates whether a platform's previously published image differs from the equivalent build
    /// being reused, which requires the platform's tags to be published against the reused image.
    /// </summary>
    private static bool HasEquivalentBuildChanged(
        PlatformData? previousPlatform,
        PlatformData? reusedPlatform) =>
        previousPlatform is not null &&
        reusedPlatform is not null &&
        !DockerHelper.GetDigestSha(previousPlatform.Digest).Equals(
            DockerHelper.GetDigestSha(reusedPlatform.Digest),
            StringComparison.OrdinalIgnoreCase);

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
