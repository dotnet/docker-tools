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
        IEnumerable<PlatformInfo> selectedPlatforms,
        ImageArtifactDetails? imageArtifactDetails,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        bool useCache)
    {
        // Build effects may pass through unselected platforms, so preserve the full dependency graph.
        HashSet<PlatformInfo> selectedPlatformSet = selectedPlatforms.ToHashSet();
        PlatformInfo[] allUniquePlatforms = [..allPlatforms.Distinct()];

        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependenciesByPlatform =
            allUniquePlatforms.ToDictionary(
                platform => platform,
                platform => (IReadOnlyList<PlatformInfo>)manifest.GetParents(platform, allUniquePlatforms).ToArray());

        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependentsByPlatform =
            GetDependentsByPlatform(allUniquePlatforms, dependenciesByPlatform);

        IReadOnlyDictionary<PlatformInfo, int> dependencyDepths =
            GetDependencyDepths(allUniquePlatforms, dependenciesByPlatform);

        PlatformInfo[] platformsToPlan = [..allUniquePlatforms.Where(selectedPlatformSet.Contains)];

        Dictionary<PlatformInfo, PlatformData?> publishedMetadataByPlatform =
            platformsToPlan.ToDictionary(
                platform => platform,
                platform => GetPublishedPlatformMetadata(manifest, platform, imageArtifactDetails));

        Dictionary<PlatformInfo, BuildDecision> initialDecisionsByPlatform = [];

        // Equivalent platforms produce one image. Plan parents first so their reusable digest is
        // available when evaluating children.
        IEnumerable<PlatformInfo[]> platformGroupsSharingImageContent = platformsToPlan
            .GroupBy(GetBuildCacheKey)
            .OrderBy(build => build.Max(platform => dependencyDepths[platform]))
            .Select(build => build.ToArray());

        foreach (PlatformInfo[] platformsSharingImageContent in platformGroupsSharingImageContent)
        {
            PlatformInfo contentPlatform =
                SelectContentPlatform(platformsSharingImageContent, publishedMetadataByPlatform);
            PlatformData? imageToReuse = publishedMetadataByPlatform[contentPlatform];

            LogSharedContentScope(logger, platformsSharingImageContent, contentPlatform);

            IReadOnlyList<BuildPlanReason> imageBuildReasons = useCache ?
                await GetContentBuildReasonsAsync(contentPlatform, imageToReuse, baseImageResolver, sourceRepoUrl) :
                [BuildPlanReason.CacheDisabled];

            bool buildImage = imageBuildReasons.Count > 0;

            foreach (PlatformInfo platform in platformsSharingImageContent)
            {
                PlatformData? publishedMetadata = publishedMetadataByPlatform[platform];
                List<BuildPlanReason> reasons = [..imageBuildReasons];

                if (useCache && !HasAllTagsPublished(publishedMetadata))
                {
                    reasons.Add(BuildPlanReason.MissingTags);
                }

                if (!buildImage && HasEquivalentBuildChanged(publishedMetadata, imageToReuse))
                {
                    reasons.Add(BuildPlanReason.EquivalentBuildChanged);
                }

                BuildDecision decision = CreateBuildDecision(platform, imageToReuse, buildImage, reasons);
                initialDecisionsByPlatform[platform] = decision;

                if (decision.ImageToReuse is not null)
                {
                    RecordAvailableImage(manifest, platform, decision.ImageToReuse, baseImageResolver);
                }
            }
        }

        // A rebuilt image invalidates its descendants even if they were otherwise reusable.
        IReadOnlyDictionary<PlatformInfo, BuildDecision> finalDecisionsByPlatform =
            PropagateBuildCauses(initialDecisionsByPlatform, dependentsByPlatform);
        BuildPlan buildPlan = CreateBuildPlan(
            allUniquePlatforms,
            dependenciesByPlatform,
            dependentsByPlatform,
            finalDecisionsByPlatform);
        LogPlan(logger, buildPlan);
        return buildPlan;
    }

    /// <summary>
    /// Logs that one content result covers several platforms, so that a single set of check results
    /// for a Dockerfile is not mistaken for a result about a single platform.
    /// </summary>
    private static void LogSharedContentScope(
        ILogger logger,
        IReadOnlyCollection<PlatformInfo> platformsSharingImageContent,
        PlatformInfo contentPlatform)
    {
        if (platformsSharingImageContent.Count > 1)
        {
            logger.LogInformation(
                "Dockerfile '{DockerfilePath}' is shared by {PlatformCount} platforms, which are planned " +
                "together: {Tags}",
                contentPlatform.DockerfilePathRelativeToManifest,
                platformsSharingImageContent.Count,
                string.Join(", ", platformsSharingImageContent.Select(DescribeTag)));
        }
    }

    /// <summary>
    /// Logs the entire plan before any action is taken on it.
    /// </summary>
    private static void LogPlan(ILogger logger, BuildPlan plan)
    {
        IReadOnlyList<BuildPlanNode> nodesWithDecisions = plan.GetNodesWithDecisionsInBuildOrder();
        logger.LogInformation(
            "Build plan: {BuildCount} to build, {ReuseCount} to reuse, {ReuseAndPublishTagsCount} to reuse " +
            "and publish tags",
            nodesWithDecisions.Count(node => node.Decision?.Action == BuildAction.Build),
            nodesWithDecisions.Count(node => node.Decision?.Action == BuildAction.Reuse),
            nodesWithDecisions.Count(node => node.Decision?.Action == BuildAction.ReuseAndPublishTags));

        foreach (BuildPlanNode node in nodesWithDecisions)
        {
            BuildDecision decision = node.Decision ??
                throw new InvalidOperationException(
                    $"Build plan did not provide a decision for '{node.Platform.DockerfilePathRelativeToManifest}'.");
            logger.LogInformation(
                "Planned {Action} of '{DockerfilePath}' ({Tag}) because {Causes}",
                decision.Action,
                node.Platform.DockerfilePathRelativeToManifest,
                DescribeTag(node.Platform),
                DescribeCauses(decision.Causes));
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

    private static PlatformData? GetPublishedPlatformMetadata(
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
    /// equivalent platforms. Published metadata is preferred so content freshness can be evaluated.
    /// </summary>
    private static PlatformInfo SelectContentPlatform(
        IEnumerable<PlatformInfo> platformsSharingImageContent,
        IReadOnlyDictionary<PlatformInfo, PlatformData?> publishedMetadataByPlatform) =>
        platformsSharingImageContent
            .OrderByDescending(platform => publishedMetadataByPlatform[platform] is not null)
            .ThenBy(platform => platform.DockerfilePathRelativeToManifest, StringComparer.Ordinal)
            .ThenBy(platform => platform.Tags.FirstOrDefault()?.Name, StringComparer.Ordinal)
            .First();

    private async Task<IReadOnlyList<BuildPlanReason>> GetContentBuildReasonsAsync(
        PlatformInfo platform,
        PlatformData? publishedMetadata,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl)
    {
        if (publishedMetadata is null)
        {
            return [BuildPlanReason.MissingImageInfo];
        }

        List<BuildPlanReason> reasons = [];

        if (await HasBaseImageChangedAsync(platform, publishedMetadata, baseImageResolver))
        {
            reasons.Add(BuildPlanReason.BaseImageChanged);
        }

        if (HasDockerfileChanged(platform, publishedMetadata, sourceRepoUrl))
        {
            reasons.Add(BuildPlanReason.DockerfileChanged);
        }

        return reasons;
    }

    private async Task<bool> HasBaseImageChangedAsync(
        PlatformInfo platform,
        PlatformData publishedMetadata,
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
        string? publishedDigestSha = publishedMetadata.BaseImageDigest is string publishedDigest
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

    private bool HasDockerfileChanged(PlatformInfo platform, PlatformData publishedMetadata, string? sourceRepoUrl)
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
            publishedMetadata.CommitUrl?.Equals(currentCommitUrl, StringComparison.OrdinalIgnoreCase) == true;

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
                publishedMetadata.CommitUrl,
                currentCommitUrl);
        }

        return !commitShaMatches;
    }

    private static bool HasAllTagsPublished(PlatformData? publishedMetadata) =>
        publishedMetadata is not null &&
        (publishedMetadata.PlatformInfo?.Tags ?? [])
            .Select(tag => tag.Name)
            .AreEquivalent(publishedMetadata.SimpleTags);

    /// <summary>
    /// Indicates whether a platform's previously published image differs from the equivalent build
    /// being reused, which requires the platform's tags to be published against the reused image.
    /// </summary>
    private static bool HasEquivalentBuildChanged(PlatformData? publishedMetadata, PlatformData? reuseSource) =>
        publishedMetadata is not null
        && reuseSource is not null
        && !DockerHelper
            .GetDigestSha(publishedMetadata.Digest)
            .Equals(DockerHelper.GetDigestSha(reuseSource.Digest), StringComparison.OrdinalIgnoreCase);

    private static BuildDecision CreateBuildDecision(
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

        return new BuildDecision(
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

    private static IReadOnlyDictionary<PlatformInfo, BuildDecision> PropagateBuildCauses(
        IReadOnlyDictionary<PlatformInfo, BuildDecision> initialDecisionsByPlatform,
        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependentsByPlatform)
    {
        Dictionary<PlatformInfo, BuildDecision> decisionsByPlatform =
            initialDecisionsByPlatform.ToDictionary(pair => pair.Key, pair => pair.Value);

        KeyValuePair<PlatformInfo, BuildDecision>[] directlyBuiltPlatforms = decisionsByPlatform
            .Where(pair => pair.Value.Action == BuildAction.Build)
            .ToArray();

        foreach ((PlatformInfo directlyBuiltPlatform, BuildDecision directlyBuiltDecision) in directlyBuiltPlatforms)
        {
            Queue<(PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path)> queue = new();
            queue.Enqueue((directlyBuiltPlatform, [directlyBuiltPlatform]));
            HashSet<PlatformInfo> visited = [directlyBuiltPlatform];

            while (queue.TryDequeue(out (PlatformInfo Platform, IReadOnlyList<PlatformInfo> Path) current))
            {
                foreach (PlatformInfo child in dependentsByPlatform[current.Platform].Where(visited.Add))
                {
                    PlatformInfo[] childPath = [..current.Path, child];

                    // A platform that wasn't evaluated has no decision to update, but the build
                    // still propagates through it to the platforms that depend on it.
                    if (decisionsByPlatform.TryGetValue(child, out BuildDecision? childDecision))
                    {
                        BuildCause[] propagatedCauses = directlyBuiltDecision.Causes
                            .Select(cause => cause with { DependencyPath = childPath })
                            .ToArray();

                        decisionsByPlatform[child] = childDecision with
                        {
                            Action = BuildAction.Build,
                            ImageToReuse = null,
                            Causes = childDecision.Causes.Concat(propagatedCauses).Distinct().ToArray()
                        };
                    }

                    queue.Enqueue((child, childPath));
                }
            }
        }

        return decisionsByPlatform;
    }

    private static BuildPlan CreateBuildPlan(
        IEnumerable<PlatformInfo> platforms,
        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependenciesByPlatform,
        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependentsByPlatform,
        IReadOnlyDictionary<PlatformInfo, BuildDecision> decisionsByPlatform)
    {
        Dictionary<PlatformInfo, BuildPlanNode> nodesByPlatform = [];

        BuildPlanNode CreateNode(PlatformInfo platform)
        {
            if (!nodesByPlatform.TryGetValue(platform, out BuildPlanNode? node))
            {
                node = new BuildPlanNode(
                    platform,
                    decisionsByPlatform.GetValueOrDefault(platform),
                    dependentsByPlatform[platform].Select(CreateNode).ToArray());
                nodesByPlatform.Add(platform, node);
            }

            return node;
        }

        BuildPlanNode[] roots = platforms
            .Where(platform => dependenciesByPlatform[platform].Count == 0)
            .Select(CreateNode)
            .ToArray();

        return new BuildPlan(roots);
    }

    private static IReadOnlyDictionary<PlatformInfo, int> GetDependencyDepths(
        IEnumerable<PlatformInfo> platforms,
        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependenciesByPlatform)
    {
        Dictionary<PlatformInfo, int> dependencyDepths = [];
        foreach (PlatformInfo platform in platforms)
        {
            GetDependencyDepth(platform);
        }

        return dependencyDepths;

        int GetDependencyDepth(PlatformInfo platform)
        {
            if (!dependencyDepths.TryGetValue(platform, out int depth))
            {
                IReadOnlyList<PlatformInfo> dependencies = dependenciesByPlatform[platform];
                depth = dependencies.Count == 0 ? 0 : dependencies.Max(GetDependencyDepth) + 1;
                dependencyDepths[platform] = depth;
            }

            return depth;
        }
    }

    private static IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> GetDependentsByPlatform(
        IEnumerable<PlatformInfo> platforms,
        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependenciesByPlatform)
    {
        Dictionary<PlatformInfo, List<PlatformInfo>> dependentsByPlatform =
            platforms.ToDictionary(platform => platform, _ => new List<PlatformInfo>());

        foreach ((PlatformInfo platform, IReadOnlyList<PlatformInfo> dependencies) in dependenciesByPlatform)
        {
            foreach (PlatformInfo dependency in dependencies)
            {
                dependentsByPlatform[dependency].Add(platform);
            }
        }

        return dependentsByPlatform.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<PlatformInfo>)pair.Value.ToArray());
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
