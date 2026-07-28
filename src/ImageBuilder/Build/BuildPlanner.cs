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
        IEnumerable<PlatformInfo> dependencyPlatforms,
        IEnumerable<PlatformInfo> platformsToPlan,
        ImageArtifactDetails? imageArtifactDetails,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        bool useCache)
    {
        // The graph includes platforms that do not receive decisions because build effects must be
        // able to travel through them to planned descendants.
        HashSet<PlatformInfo> platformsToPlanSet = platformsToPlan.ToHashSet();
        PlatformInfo[] dependencyGraphPlatforms = [..dependencyPlatforms.Distinct()];
        PlatformDependencyGraph dependencyGraph =
            PlatformDependencyGraph.Create(manifest, dependencyGraphPlatforms);
        PlatformInfo[] platformsToPlanInGraphOrder =
            [..dependencyGraphPlatforms.Where(platformsToPlanSet.Contains)];

        // Published metadata is indexed once because both shared-content and per-platform
        // publication decisions consult it.
        Dictionary<PlatformInfo, PlatformData?> publishedPlatformByPlatform =
            platformsToPlanInGraphOrder.ToDictionary(
                platform => platform,
                platform => GetPublishedPlatform(manifest, platform, imageArtifactDetails));

        // Equivalent platforms produce the same image content. Plan each equivalent build once,
        // parent-first, then make the publication decision for each platform that shares it.
        Dictionary<PlatformInfo, PlannedPlatform> directPlanByPlatform = [];
        foreach (PlatformInfo[] equivalentBuildPlatforms in
            GetEquivalentBuildsInDependencyOrder(platformsToPlanInGraphOrder, dependencyGraph))
        {
            foreach (PlannedPlatform plannedPlatform in await PlanEquivalentBuildAsync(
                manifest,
                equivalentBuildPlatforms,
                publishedPlatformByPlatform,
                baseImageResolver,
                sourceRepoUrl,
                useCache))
            {
                directPlanByPlatform.Add(plannedPlatform.Platform, plannedPlatform);
            }
        }

        // Direct decisions describe each platform in isolation. The final pass turns descendants
        // of rebuilt images into builds and records the dependency path that caused each change.
        PlannedPlatform[] directPlan =
            [..platformsToPlanInGraphOrder.Select(platform => directPlanByPlatform[platform])];
        IReadOnlyList<PlannedPlatform> propagatedPlan =
            PropagateBuildCauses(directPlan, dependencyGraph);
        BuildPlan plan = new(propagatedPlan, dependencyGraph);
        LogPlan(logger, plan);
        return plan;
    }

    /// <summary>
    /// Plans one image build and the individual publication state of every platform that shares its
    /// Dockerfile and build arguments.
    /// </summary>
    private async Task<IReadOnlyList<PlannedPlatform>> PlanEquivalentBuildAsync(
        ManifestInfo manifest,
        IReadOnlyCollection<PlatformInfo> equivalentBuildPlatforms,
        IReadOnlyDictionary<PlatformInfo, PlatformData?> publishedPlatformByPlatform,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        bool useCache)
    {
        PlatformInfo contentEvaluationPlatform = SelectContentEvaluationPlatform(
            equivalentBuildPlatforms,
            publishedPlatformByPlatform);
        PlatformData? reuseSource = publishedPlatformByPlatform[contentEvaluationPlatform];

        LogSharedContentScope(logger, equivalentBuildPlatforms, contentEvaluationPlatform);

        IReadOnlyList<BuildPlanReason> sharedContentBuildReasons = useCache
            ? await GetContentBuildReasonsAsync(
                contentEvaluationPlatform,
                reuseSource,
                baseImageResolver,
                sourceRepoUrl)
            : [BuildPlanReason.CacheDisabled];

        bool sharedContentMustBeBuilt = sharedContentBuildReasons.Count > 0;
        List<PlannedPlatform> plannedPlatforms = [];

        foreach (PlatformInfo platform in equivalentBuildPlatforms)
        {
            PlatformData? publishedPlatform = publishedPlatformByPlatform[platform];
            List<BuildPlanReason> actionReasons = [..sharedContentBuildReasons];

            if (useCache && !HasAllTagsPublished(publishedPlatform))
            {
                actionReasons.Add(BuildPlanReason.MissingTags);
            }

            if (!sharedContentMustBeBuilt &&
                HasEquivalentBuildChanged(publishedPlatform, reuseSource))
            {
                actionReasons.Add(BuildPlanReason.EquivalentBuildChanged);
            }

            PlannedPlatform plannedPlatform = CreatePlannedPlatform(
                platform,
                reuseSource,
                sharedContentMustBeBuilt,
                actionReasons);
            plannedPlatforms.Add(plannedPlatform);

            // A reused image becomes an available base image for groups planned later.
            if (plannedPlatform.ImageToReuse is not null)
            {
                RecordAvailableImage(
                    manifest,
                    platform,
                    plannedPlatform.ImageToReuse,
                    baseImageResolver);
            }
        }

        return plannedPlatforms;
    }

    /// <summary>
    /// Logs that one content result covers several platforms, so that a single set of check results
    /// for a Dockerfile is not mistaken for a result about a single platform.
    /// </summary>
    private static void LogSharedContentScope(
        ILogger logger,
        IReadOnlyCollection<PlatformInfo> equivalentBuildPlatforms,
        PlatformInfo contentEvaluationPlatform)
    {
        if (equivalentBuildPlatforms.Count > 1)
        {
            logger.LogInformation(
                "Dockerfile '{DockerfilePath}' is shared by {PlatformCount} platforms, which are planned together: {Tags}",
                contentEvaluationPlatform.DockerfilePathRelativeToManifest,
                equivalentBuildPlatforms.Count,
                string.Join(", ", equivalentBuildPlatforms.Select(DescribeTag)));
        }
    }

    /// <summary>
    /// Logs the entire plan before any action is taken on it.
    /// </summary>
    private static void LogPlan(ILogger logger, BuildPlan plan)
    {
        logger.LogInformation(
            "Build plan: {BuildCount} to build, {ReuseCount} to reuse, {ReuseAndPublishTagsCount} to reuse and publish tags",
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

    private static PlatformData? GetPublishedPlatform(
        ManifestInfo manifest,
        PlatformInfo platform,
        ImageArtifactDetails? imageArtifactDetails)
    {
        if (imageArtifactDetails is null)
        {
            return null;
        }

        RepoInfo repo = manifest.GetRepoByImage(manifest.GetImageByPlatform(platform));
        return ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails)?.Platform;
    }

    /// <summary>
    /// Selects the platform whose previously published metadata represents the content shared by
    /// an equivalent build. Platforms with previously published metadata are preferred so that
    /// content freshness can be evaluated.
    /// </summary>
    private static PlatformInfo SelectContentEvaluationPlatform(
        IEnumerable<PlatformInfo> equivalentBuildPlatforms,
        IReadOnlyDictionary<PlatformInfo, PlatformData?> publishedPlatformByPlatform) =>
        equivalentBuildPlatforms
            .OrderByDescending(platform => publishedPlatformByPlatform[platform] is not null)
            .ThenBy(platform => platform.DockerfilePathRelativeToManifest, StringComparer.Ordinal)
            .ThenBy(platform => platform.Tags.FirstOrDefault()?.Name, StringComparer.Ordinal)
            .First();

    private static IEnumerable<PlatformInfo[]> GetEquivalentBuildsInDependencyOrder(
        IEnumerable<PlatformInfo> platforms,
        PlatformDependencyGraph dependencyGraph) =>
        platforms
            .GroupBy(GetBuildCacheKey)
            .OrderBy(equivalentBuild =>
                equivalentBuild.Max(dependencyGraph.GetDependencyDepth))
            .Select(equivalentBuild => equivalentBuild.ToArray());

    private async Task<IReadOnlyList<BuildPlanReason>> GetContentBuildReasonsAsync(
        PlatformInfo platform,
        PlatformData? publishedPlatform,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl)
    {
        if (publishedPlatform is null)
        {
            return [BuildPlanReason.MissingImageInfo];
        }

        List<BuildPlanReason> reasons = [];

        if (await HasBaseImageChangedAsync(platform, publishedPlatform, baseImageResolver))
        {
            reasons.Add(BuildPlanReason.BaseImageChanged);
        }

        if (HasDockerfileChanged(platform, publishedPlatform, sourceRepoUrl))
        {
            reasons.Add(BuildPlanReason.DockerfileChanged);
        }

        return reasons;
    }

    private async Task<bool> HasBaseImageChangedAsync(
        PlatformInfo platform,
        PlatformData publishedPlatform,
        BaseImageResolver baseImageResolver)
    {
        if (platform.FinalStageFromImage is null)
        {
            logger.LogInformation(
                "Dockerfile '{DockerfilePath}' has no base image, so it is considered up-to-date",
                platform.DockerfilePathRelativeToManifest);

            return false;
        }

        string? currentDigestSha = await baseImageResolver.ResolveDigestShaAsync(platform);
        string? publishedDigestSha = publishedPlatform.BaseImageDigest is string publishedDigest
            ? DockerHelper.GetDigestSha(publishedDigest)
            : null;

        if (publishedDigestSha?.Equals(currentDigestSha, StringComparison.OrdinalIgnoreCase) == true)
        {
            logger.LogInformation(
                "Base image of '{DockerfilePath}' is unchanged at digest {BaseImageDigestSha}",
                platform.DockerfilePathRelativeToManifest,
                currentDigestSha);

            return false;
        }

        logger.LogInformation(
            "Base image of '{DockerfilePath}' changed from digest {PreviousBaseImageDigestSha} to " +
            "{CurrentBaseImageDigestSha}",
            platform.DockerfilePathRelativeToManifest,
            publishedDigestSha,
            currentDigestSha);

        return true;
    }

    private bool HasDockerfileChanged(PlatformInfo platform, PlatformData publishedPlatform, string? sourceRepoUrl)
    {
        // Comparing Dockerfile commits requires the Dockerfile to be present on disk and a source
        // repo URL to form the commit URL recorded in image info. Contexts that plan against a
        // remote manifest have neither, so the Dockerfile is considered unchanged.
        if (sourceRepoUrl is null)
        {
            return false;
        }

        string currentCommitUrl = gitService.GetDockerfileCommitUrl(platform, sourceRepoUrl);

        bool commitShaMatches =
            publishedPlatform.CommitUrl?.Equals(currentCommitUrl, StringComparison.OrdinalIgnoreCase) == true;

        if (commitShaMatches)
        {
            logger.LogInformation(
                "Dockerfile '{DockerfilePath}' is unchanged since commit {CommitUrl}",
                platform.DockerfilePathRelativeToManifest,
                currentCommitUrl);
        }
        else
        {
            logger.LogInformation(
                "Dockerfile '{DockerfilePath}' changed from commit {PreviousCommitUrl} to {CurrentCommitUrl}",
                platform.DockerfilePathRelativeToManifest,
                publishedPlatform.CommitUrl,
                currentCommitUrl);
        }

        return !commitShaMatches;
    }

    private static bool HasAllTagsPublished(PlatformData? publishedPlatform) =>
        publishedPlatform is not null &&
        (publishedPlatform.PlatformInfo?.Tags ?? [])
            .Select(tag => tag.Name)
            .AreEquivalent(publishedPlatform.SimpleTags);

    /// <summary>
    /// Indicates whether a platform's previously published image differs from the equivalent build
    /// being reused, which requires the platform's tags to be published against the reused image.
    /// </summary>
    private static bool HasEquivalentBuildChanged(PlatformData? publishedPlatform, PlatformData? reuseSource) =>
        publishedPlatform is not null
        && reuseSource is not null
        && !DockerHelper
            .GetDigestSha(publishedPlatform.Digest)
            .Equals(DockerHelper.GetDigestSha(reuseSource.Digest), StringComparison.OrdinalIgnoreCase);

    private static PlannedPlatform CreatePlannedPlatform(
        PlatformInfo platform,
        PlatformData? imageToReuse,
        bool requiresBuild,
        IReadOnlyCollection<BuildPlanReason> reasons)
    {
        BuildAction action = requiresBuild ?
            BuildAction.Build :
            reasons.Count > 0 ?
                BuildAction.ReuseAndPublishTags :
                BuildAction.Reuse;

        if (action is BuildAction.Reuse or BuildAction.ReuseAndPublishTags && imageToReuse is null)
        {
            throw new InvalidOperationException(
                $"Build planning produced '{action}' for " +
                $"'{platform.DockerfilePathRelativeToManifest}' without cached image metadata.");
        }

        return new PlannedPlatform(
            Platform: platform,
            Action: action,
            ImageToReuse: action is BuildAction.Build ? null : imageToReuse,
            Causes: reasons.Select(reason => CreateDirectCause(platform, reason)).ToArray());
    }

    private static BuildCause CreateDirectCause(PlatformInfo platform, BuildPlanReason reason) =>
        new(reason, platform, [platform]);

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
        Dictionary<PlatformInfo, PlannedPlatform> propagatedPlanByPlatform =
            plannedPlatforms.ToDictionary(planned => planned.Platform);
        PlannedPlatform[] buildOrigins = propagatedPlanByPlatform.Values
            .Where(planned => planned.Action == BuildAction.Build)
            .ToArray();

        foreach (PlannedPlatform buildOrigin in buildOrigins)
        {
            Queue<(PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path)> queue = new();
            queue.Enqueue((buildOrigin.Platform, [buildOrigin.Platform]));
            HashSet<PlatformInfo> visited = [buildOrigin.Platform];

            while (queue.TryDequeue(out (PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path) current))
            {
                foreach (PlatformInfo child in dependencyGraph.GetChildren(current.Platform).Where(visited.Add))
                {
                    PlatformInfo[] childPath = [..current.Path, child];

                    // A platform without a direct decision still carries the build effect to its
                    // descendants.
                    if (propagatedPlanByPlatform.TryGetValue(
                        child,
                        out PlannedPlatform? descendantPlan))
                    {
                        BuildCause[] propagatedCauses = buildOrigin.Causes
                            .Select(cause => cause with { DependencyPath = childPath })
                            .ToArray();

                        propagatedPlanByPlatform[child] = descendantPlan with
                        {
                            Action = BuildAction.Build,
                            ImageToReuse = null,
                            Causes = descendantPlan.Causes
                                .Concat(propagatedCauses)
                                .Distinct()
                                .ToArray()
                        };
                    }

                    queue.Enqueue((child, childPath));
                }
            }
        }

        return plannedPlatforms
            .Select(planned => propagatedPlanByPlatform[planned.Platform])
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
