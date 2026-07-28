// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <inheritdoc/>
public class BuildPlanner(ILogger<BuildPlanner> logger, IGitService gitService) : IBuildPlanner
{
    /// <inheritdoc/>
    public async Task<BuildPlan> CreateBuildPlanAsync(
        ManifestInfo manifest,
        IEnumerable<PlatformInfo> allPlatforms,
        IEnumerable<PlatformInfo> platformsToEvaluate,
        ImageArtifactDetails? imageArtifactDetails,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        IReadOnlyList<IBuildPlanCheck> checks)
    {
        HashSet<PlatformInfo> evaluatedPlatforms = platformsToEvaluate.ToHashSet();
        PlatformInfo[] graphPlatforms = [..allPlatforms.Distinct()];
        PlatformDependencyGraph dependencyGraph =
            PlatformDependencyGraph.Create(manifest, graphPlatforms);

        PlatformInfo[] evaluationOrder = [..graphPlatforms.Where(evaluatedPlatforms.Contains)];
        Dictionary<PlatformInfo, PlatformData?> previousPlatforms = evaluationOrder.ToDictionary(
            platform => platform,
            platform => GetPreviousPlatform(manifest, platform, imageArtifactDetails));
        Dictionary<PlatformInfo, PlannedPlatform> plannedByPlatform = [];

        IBuildPlanCheck[] contentChecks =
            [..checks.Where(check => check.Scope is BuildPlanCheckScope.ImageContent)];
        IBuildPlanCheck[] publishChecks =
            [..checks.Where(check => check.Scope is BuildPlanCheckScope.PlatformPublication)];

        // Platforms that share a Dockerfile and build args produce the same image content, so the
        // content checks are evaluated once per group. Groups are evaluated in dependency order so
        // that a parent's reuse decision is recorded before its children resolve their base image.
        IEnumerable<IGrouping<string, PlatformInfo>> platformGroups = evaluationOrder
            .GroupBy(GetBuildCacheKey)
            .OrderBy(group => group.Max(dependencyGraph.GetDependencyDepth));

        foreach (IGrouping<string, PlatformInfo> group in platformGroups)
        {
            PlatformInfo[] groupPlatforms = [..group];
            PlatformInfo representative = SelectContentRepresentative(groupPlatforms, previousPlatforms);
            PlatformData? reusedPlatform = previousPlatforms[representative];

            LogSharedContentScope(logger, groupPlatforms, representative);

            IReadOnlyList<EvaluatedBuildPlanCheck> contentResults = await EvaluateChecksAsync(
                contentChecks,
                CreateContext(representative, reusedPlatform));
            bool requiresBuild = contentResults.Any(result =>
                result.Disposition == BuildPlanCheckDisposition.Build);

            foreach (PlatformInfo platform in groupPlatforms)
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

                PlannedPlatform planned = CreatePlannedPlatform(platform, reusedPlatform, results);
                plannedByPlatform[platform] = planned;

                if (planned.ImageToReuse is not null)
                {
                    RecordAvailableImage(
                        manifest,
                        platform,
                        planned.ImageToReuse,
                        baseImageResolver);
                }
            }
        }

        PlannedPlatform[] plannedResults =
            [..evaluationOrder.Select(platform => plannedByPlatform[platform])];
        BuildPlan plan = new(PropagateBuildCauses(plannedResults, dependencyGraph), dependencyGraph);
        LogPlan(logger, plan);
        return plan;

        BuildPlanCheckContext CreateContext(PlatformInfo platform, PlatformData? previousPlatform) =>
            new(platform, previousPlatform, baseImageResolver, gitService, logger, sourceRepoUrl);
    }

    /// <summary>
    /// Logs that one content result covers several platforms, so that a single set of check results
    /// for a Dockerfile is not mistaken for a result about a single platform.
    /// </summary>
    private static void LogSharedContentScope(
        ILogger logger,
        IReadOnlyCollection<PlatformInfo> group,
        PlatformInfo representative)
    {
        if (group.Count > 1)
        {
            logger.LogInformation(
                "Dockerfile '{DockerfilePath}' is shared by {PlatformCount} platforms, which are planned "
                    + "together: {Tags}",
                representative.DockerfilePathRelativeToManifest,
                group.Count,
                string.Join(", ", group.Select(DescribeTag)));
        }
    }

    /// <summary>
    /// Logs the entire plan before any action is taken on it.
    /// </summary>
    private static void LogPlan(ILogger logger, BuildPlan plan)
    {
        logger.LogInformation(
            "Build plan: {BuildCount} to build, {ReuseCount} to reuse, {ReuseAndPublishTagsCount} to "
                + "reuse and publish tags",
            plan.Platforms.Count(planned => planned.Action == BuildAction.Build),
            plan.Platforms.Count(planned => planned.Action == BuildAction.Reuse),
            plan.Platforms.Count(planned => planned.Action == BuildAction.ReuseAndPublishTags));

        foreach (PlannedPlatform planned in plan.Platforms)
        {
            logger.LogInformation(
                "Planned {Action} of '{DockerfilePath}' ({Tag}) because {Causes}",
                planned.Action,
                planned.Platform.DockerfilePathRelativeToManifest,
                DescribeTag(planned.Platform),
                DescribeCauses(planned.Causes));
        }
    }

    /// <summary>
    /// Describes why a platform received its action. A cause that was propagated from a platform it
    /// depends on includes the path it was propagated along.
    /// </summary>
    private static string DescribeCauses(IReadOnlyList<BuildCause> causes) =>
        causes.Count == 0
            ? "nothing about it changed"
            : string.Join(
                ", ",
                causes.Select(cause => !cause.IsDirect()
                    ? $"{cause.Reason} via " + string.Join(
                        " -> ",
                        cause.DependencyPath.Select(platform => platform.DockerfilePathRelativeToManifest))
                    : cause.Reason.ToString()));

    /// <summary>
    /// Describes a platform by its first tag, which distinguishes manifest entries that share a
    /// Dockerfile.
    /// </summary>
    private static string DescribeTag(PlatformInfo platform) =>
        platform.Tags.FirstOrDefault()?.FullyQualifiedName ?? "untagged";

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

    private static PlannedPlatform CreatePlannedPlatform(
        PlatformInfo platform,
        PlatformData? imageToReuse,
        IReadOnlyCollection<EvaluatedBuildPlanCheck> results)
    {
        BuildAction action = results.Any(result =>
            result.Disposition == BuildPlanCheckDisposition.Build) ?
            BuildAction.Build :
            results.Any(result =>
                result.Disposition == BuildPlanCheckDisposition.ReuseAndPublish) ?
                BuildAction.ReuseAndPublishTags :
                BuildAction.Reuse;

        if (action is BuildAction.Reuse or BuildAction.ReuseAndPublishTags &&
            imageToReuse is null)
        {
            throw new InvalidOperationException(
                $"Planning checks produced '{action}' for " +
                $"'{platform.DockerfilePathRelativeToManifest}' without cached image metadata.");
        }

        return new PlannedPlatform(
            platform,
            action,
            action is BuildAction.Build ? null : imageToReuse,
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

    private static IReadOnlyList<PlannedPlatform> PropagateBuildCauses(
        IEnumerable<PlannedPlatform> plannedPlatforms,
        PlatformDependencyGraph dependencyGraph)
    {
        Dictionary<PlatformInfo, PlannedPlatform> plannedByPlatform =
            plannedPlatforms.ToDictionary(planned => planned.Platform);
        PlannedPlatform[] directlyBuiltPlatforms = plannedByPlatform.Values
            .Where(planned => planned.Action == BuildAction.Build)
            .ToArray();

        foreach (PlannedPlatform directlyBuilt in directlyBuiltPlatforms)
        {
            Queue<(PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path)> queue = new();
            queue.Enqueue((directlyBuilt.Platform, [directlyBuilt.Platform]));
            HashSet<PlatformInfo> visited = [directlyBuilt.Platform];

            while (queue.TryDequeue(out (PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path) current))
            {
                foreach (PlatformInfo child in
                    dependencyGraph.GetChildren(current.Platform).Where(visited.Add))
                {
                    PlatformInfo[] childPath = [..current.Path, child];

                    // A platform that wasn't evaluated has no decision to update, but the build
                    // still propagates through it to the platforms that depend on it.
                    if (plannedByPlatform.TryGetValue(child, out PlannedPlatform? childPlanned))
                    {
                        BuildCause[] propagatedCauses = directlyBuilt.Causes
                            .Select(cause => cause with { DependencyPath = childPath })
                            .ToArray();

                        plannedByPlatform[child] = childPlanned with
                        {
                            Action = BuildAction.Build,
                            ImageToReuse = null,
                            Causes = childPlanned.Causes.Concat(propagatedCauses).Distinct().ToArray()
                        };
                    }

                    queue.Enqueue((child, childPath));
                }
            }
        }

        return plannedPlatforms
            .Select(planned => plannedByPlatform[planned.Platform])
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
