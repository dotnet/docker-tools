// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Calculates the image work required for a build without executing that work.
/// </summary>
public class BuildPlanner(ILogger<BuildPlanner> logger)
{
    public virtual async Task<BuildPlanItem[]> CreatePlanAsync(
        BuildGraph graph,
        ImageArtifactDetails? imageInfo,
        IBuildPolicy policy,
        CancellationToken cancellationToken = default)
    {
        Dictionary<BuildTarget, PublishedImage> publishedImages = CreatePublishedImageIndex(graph, imageInfo);
        Dictionary<BuildTarget, BuildPlanItem> planItems = [];

        // Shared builds act as one node. Evaluate and apply each node from roots to leaves so
        // every parent decision is final before its children are considered.
        foreach (var sharedBuildTargets in GetSharedBuildsInDependencyOrder(graph))
        {
            foreach (BuildTarget target in sharedBuildTargets)
            {
                var context = new BuildPolicyContext(graph, target, publishedImages);
                BuildPolicyResult decision = await policy.EvaluateAsync(context, cancellationToken);

                publishedImages.TryGetValue(target, out PublishedImage? publishedImage);
                planItems.Add(target, CreateItem(target, decision, publishedImage));
            }

            PropagateBuildsFromParents(graph, sharedBuildTargets, planItems);
            UnifySharedBuildActions(sharedBuildTargets, planItems);
        }

        // Built images need their direct internal parents available locally. An unchanged parent
        // can use its published image; it does not need its own parents because it is not rebuilt.
        UsePublishedParentsForBuilds(graph, planItems, publishedImages);

        BuildPlanItem[] plan = graph.Targets.Select(target => planItems[target]).ToArray();
        LogPlan(plan);
        return plan;
    }

    /// <summary>
    /// Finds previously published image data for each build target.
    /// </summary>
    private static Dictionary<BuildTarget, PublishedImage> CreatePublishedImageIndex(
        BuildGraph graph,
        ImageArtifactDetails? imageInfo)
    {
        Dictionary<PlatformInfo, BuildTarget> targetsByPlatform =
            graph.Targets.ToDictionary(target => target.Platform);

        Dictionary<BuildTarget, PublishedImage> publishedImages =
            imageInfo?.Repos
                // Collect (image, platform) pairs for each platform
                .SelectMany(repo => repo.Images)
                .SelectMany(
                    image => image.Platforms.Select(
                        platform => (Platform: platform, SharedTags: image.Manifest?.SharedTags?.ToArray() ?? [])
                    )
                )
                .Where(item =>
                    // PlatformInfo is only set when a published platform matches one in the current manifest.
                    item.Platform.PlatformInfo is not null
                    // Only select platforms that are part of the current build graph.
                    && targetsByPlatform.ContainsKey(item.Platform.PlatformInfo)
                )
                .ToDictionary(
                    // SAFETY: PlatformInfo is checked in the Where clause above.
                    item => targetsByPlatform[item.Platform.PlatformInfo!],
                    item => new PublishedImage(
                        targetsByPlatform[item.Platform.PlatformInfo!],
                        item.Platform,
                        item.SharedTags)
                )
            ?? [];

        // Equivalent targets can reuse one another's published image when only one target has
        // a direct image-info entry.
        foreach (IReadOnlyList<BuildTarget> sharedBuild in graph.SharedBuildTargets.Values.Distinct())
        {
            BuildTarget? source = sharedBuild.FirstOrDefault(publishedImages.ContainsKey);

            if (source is null)
                continue;

            foreach (BuildTarget target in sharedBuild)
            {
                publishedImages.TryAdd(
                    key: target,
                    value: new PublishedImage(
                        source,
                        publishedImages[source].Image,
                        publishedImages[source].SharedTags));
            }
        }

        return publishedImages;
    }

    private static List<IReadOnlyList<BuildTarget>> GetSharedBuildsInDependencyOrder(BuildGraph graph)
    {
        IReadOnlyList<BuildTarget>[] sharedBuilds = graph.SharedBuildTargets.Values
            .DistinctBy(targets => targets[0])
            .ToArray();

        Dictionary<BuildTarget, IReadOnlyList<BuildTarget>> sharedBuildsByTarget =
            sharedBuilds
                .SelectMany(sharedBuild => sharedBuild.Select(target => (Target: target, SharedBuild: sharedBuild)))
                .ToDictionary(item => item.Target, item => item.SharedBuild);

        List<IReadOnlyList<BuildTarget>> ordered = [];
        HashSet<BuildTarget> visiting = [];
        HashSet<BuildTarget> visited = [];

        void Visit(IReadOnlyList<BuildTarget> sharedBuild)
        {
            BuildTarget key = sharedBuild[0];

            if (visited.Contains(key))
                return;

            if (!visiting.Add(key))
                throw new InvalidOperationException($"Build dependency cycle detected at '{key.DisplayName}'.");

            var parents = sharedBuild.SelectMany(target => graph.Parents[target]).Distinct();

            foreach (BuildTarget parent in parents)
            {
                IReadOnlyList<BuildTarget> parentBuild = sharedBuildsByTarget[parent];
                if (parentBuild[0] != key)
                    Visit(parentBuild);
            }

            visiting.Remove(key);
            visited.Add(key);
            ordered.Add(sharedBuild);
        }

        foreach (IReadOnlyList<BuildTarget> sharedBuild in sharedBuilds)
        {
            Visit(sharedBuild);
        }

        return ordered;
    }

    private static void PropagateBuildsFromParents(
        BuildGraph graph,
        IEnumerable<BuildTarget> sharedBuild,
        Dictionary<BuildTarget, BuildPlanItem> items)
    {
        foreach (BuildTarget target in sharedBuild)
        {
            BuildPlanItem item = items[target];

            if (item.Decision.Action == BuildAction.BuildImage)
                continue;

            BuildTarget? parent = graph.Parents[target]
                .FirstOrDefault(parent => items[parent].Decision.Action == BuildAction.BuildImage);

            if (parent is null)
                continue;

            items[target] = item with
            {
                Decision = new BuildPolicyResult(
                    BuildAction.BuildImage,
                    new BuildReason(
                        $"Dependency '{parent.DisplayName}' must build.",
                        items[parent].Decision.Reason))
            };
        }
    }

    private static void UnifySharedBuildActions(
        IEnumerable<BuildTarget> sharedBuild,
        Dictionary<BuildTarget, BuildPlanItem> items)
    {
        BuildPlanItem? invalidatedItem = sharedBuild
            .Select(target => items[target])
            .FirstOrDefault(item => item.Decision.Action == BuildAction.BuildImage);

        if (invalidatedItem is null)
            return;

        foreach (BuildTarget target in sharedBuild)
        {
            BuildPlanItem item = items[target];

            if (item.Decision.Action == BuildAction.BuildImage)
                continue;

            items[target] = item with
            {
                Decision = new BuildPolicyResult(
                    BuildAction.BuildImage,
                    new BuildReason(
                        $"Equivalent target '{invalidatedItem.Target.DisplayName}' must build.",
                        invalidatedItem.Decision.Reason))
            };
        }
    }

    private static void UsePublishedParentsForBuilds(
        BuildGraph graph,
        Dictionary<BuildTarget, BuildPlanItem> items,
        Dictionary<BuildTarget, PublishedImage> publishedImages)
    {
        var childrenToBuild = items.Values
            .Where(item => item.Decision.Action == BuildAction.BuildImage)
            .ToArray();

        foreach (BuildPlanItem childItem in childrenToBuild)
        {
            foreach (BuildTarget parent in graph.Parents[childItem.Target])
            {
                BuildPlanItem parentItem = items[parent];

                if (parentItem.Decision.Action == BuildAction.NoAction)
                {
                    if (!publishedImages.ContainsKey(parent))
                        throw new InvalidOperationException(
                            $"Required dependency '{parent.DisplayName}' has no published image.");

                    items[parent] = parentItem with
                    {
                        Decision = new BuildPolicyResult(
                            Action: BuildAction.UsePublishedImage,
                            Reason: new BuildReason(
                                $"The image is required by '{childItem.Target.DisplayName}'.",
                                childItem.Decision.Reason))
                    };
                }
            }
        }
    }

    private static BuildPlanItem CreateItem(
        BuildTarget target,
        BuildPolicyResult decision,
        PublishedImage? publishedImage)
    {
        if ((decision.Action is BuildAction.UsePublishedImage or BuildAction.PublishExistingImage)
            && publishedImage is null)
        {
            throw new InvalidOperationException(
                $"Planning selected '{decision.Action}' for '{target.DisplayName}' " +
                "without a published image.");
        }

        return new BuildPlanItem(target, decision, publishedImage);
    }

    private void LogPlan(IEnumerable<BuildPlanItem> plan)
    {
        foreach (BuildPlanItem item in plan)
        {
            logger.LogInformation(
                "Build plan for {BuildTarget}: {Action}. {Reason}",
                item.Target.DisplayName,
                item.Decision.Action,
                item.Decision.Reason);
        }
    }
}
