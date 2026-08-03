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
    private readonly ILogger<BuildPlanner> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public virtual async Task<BuildPlanItem[]> CreatePlanAsync(
        BuildGraph graph,
        ImageArtifactDetails? imageInfo,
        IBuildPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(policy);

        Dictionary<BuildTarget, PublishedImage> publishedImages =
            CreatePublishedImageIndex(graph, imageInfo);

        Dictionary<BuildTarget, BuildPlanItem> items = [];

        // Shared builds act as one node. Evaluate and apply each node from roots to leaves so
        // every parent decision is final before its children are considered.
        foreach (IReadOnlyList<BuildTarget> sharedBuild in
            GetSharedBuildsInDependencyOrder(graph))
        {
            foreach (BuildTarget target in sharedBuild)
            {
                items.Add(
                    target,
                    await EvaluateAsync(
                        graph,
                        target,
                        publishedImages,
                        policy,
                        cancellationToken));
            }

            PropagateBuildsFromParents(graph, sharedBuild, items);
            UnifySharedBuildActions(sharedBuild, items);
        }

        // Built images need their direct internal parents available locally. An unchanged parent
        // can use its published image; it does not need its own parents because it is not rebuilt.
        UsePublishedParentsForBuilds(graph, items, publishedImages);

        BuildPlanItem[] plan = graph.Targets
            .Select(target => items[target])
            .ToArray();
        LogPlan(plan);
        return plan;
    }

    private static Dictionary<BuildTarget, PublishedImage> CreatePublishedImageIndex(
        BuildGraph graph,
        ImageArtifactDetails? imageInfo)
    {
        Dictionary<PlatformInfo, BuildTarget> targetsByPlatform = graph.Targets.ToDictionary(
            target => target.Platform);
        Dictionary<BuildTarget, PublishedImage> publishedImages = imageInfo?.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(image => image.Platforms.Select(platform =>
                    (
                        Platform: platform,
                        SharedTags: image.Manifest?.SharedTags?.ToArray() ?? [])))
                .Where(item =>
                    item.Platform.PlatformInfo is not null &&
                    targetsByPlatform.ContainsKey(item.Platform.PlatformInfo))
                .GroupBy(item => item.Platform.PlatformInfo!)
                .ToDictionary(
                    group => targetsByPlatform[group.Key],
                    group =>
                    {
                        var item = group.First();
                        BuildTarget target = targetsByPlatform[group.Key];
                        return new PublishedImage(
                            target,
                            item.Platform,
                            item.SharedTags);
                    })
            ?? [];

        // Equivalent targets can reuse one another's published image when only one target has
        // a direct image-info entry.
        foreach (IReadOnlyList<BuildTarget> sharedBuild in
            graph.SharedBuildTargets.Values.Distinct())
        {
            BuildTarget? source = sharedBuild.FirstOrDefault(publishedImages.ContainsKey);
            if (source is null)
            {
                continue;
            }

            foreach (BuildTarget target in sharedBuild)
            {
                publishedImages.TryAdd(
                    target,
                    new PublishedImage(
                        source,
                        publishedImages[source].Image,
                        publishedImages[source].SharedTags));
            }
        }

        return publishedImages;
    }

    private static async Task<BuildPlanItem> EvaluateAsync(
        BuildGraph graph,
        BuildTarget target,
        IReadOnlyDictionary<BuildTarget, PublishedImage> publishedImages,
        IBuildPolicy policy,
        CancellationToken cancellationToken)
    {
        bool hasPublishedImage = publishedImages.TryGetValue(
            target,
            out PublishedImage? publishedImage);
        List<BuildReason> reasons = [];
        if (publishedImage is not null && publishedImage.Source != target)
        {
            reasons.Add(new(
                $"Published image metadata is shared with '{GetName(publishedImage.Source)}'."));
        }

        BuildPolicyResult result = await policy.EvaluateAsync(
            new(graph, target, publishedImages),
            cancellationToken);
        reasons.AddRange(result.Reasons);
        return CreateItem(
            target,
            result.Action,
            reasons,
            hasPublishedImage ? publishedImage : null);
    }

    private static List<IReadOnlyList<BuildTarget>>
        GetSharedBuildsInDependencyOrder(BuildGraph graph)
    {
        IReadOnlyList<BuildTarget>[] sharedBuilds = graph.SharedBuildTargets
            .Values
            .DistinctBy(targets => targets[0])
            .ToArray();
        Dictionary<BuildTarget, IReadOnlyList<BuildTarget>> sharedBuildByTarget =
            sharedBuilds
                .SelectMany(sharedBuild => sharedBuild.Select(
                    target => (Target: target, SharedBuild: sharedBuild)))
                .ToDictionary(item => item.Target, item => item.SharedBuild);
        List<IReadOnlyList<BuildTarget>> ordered = [];
        HashSet<BuildTarget> visiting = [];
        HashSet<BuildTarget> visited = [];

        void Visit(IReadOnlyList<BuildTarget> sharedBuild)
        {
            BuildTarget key = sharedBuild[0];
            if (visited.Contains(key))
            {
                return;
            }

            if (!visiting.Add(key))
            {
                throw new InvalidOperationException(
                    $"Build dependency cycle detected at '{GetName(key)}'.");
            }

            foreach (BuildTarget parent in sharedBuild
                .SelectMany(target => graph.Parents[target])
                .Distinct())
            {
                IReadOnlyList<BuildTarget> parentBuild = sharedBuildByTarget[parent];
                if (parentBuild[0] != key)
                {
                    Visit(parentBuild);
                }
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
        IDictionary<BuildTarget, BuildPlanItem> items)
    {
        foreach (BuildTarget target in sharedBuild)
        {
            foreach (BuildTarget parent in graph.Parents[target]
                .Where(parent => items[parent].Action == BuildAction.BuildImage))
            {
                BuildPlanItem item = items[target];
                BuildReason reason = new(
                    $"Dependency '{GetName(parent)}' must build.",
                    GetCause(items[parent]));
                items[target] = item with
                {
                    Action = BuildAction.BuildImage,
                    Reasons = item.Reasons.Contains(reason)
                        ? item.Reasons
                        : [..item.Reasons, reason]
                };
            }
        }
    }

    private static void UnifySharedBuildActions(
        IEnumerable<BuildTarget> sharedBuild,
        IDictionary<BuildTarget, BuildPlanItem> items)
    {
        BuildPlanItem? invalidatedItem = sharedBuild
            .Select(target => items[target])
            .FirstOrDefault(item => item.Action == BuildAction.BuildImage);
        if (invalidatedItem is null)
        {
            return;
        }

        foreach (BuildTarget target in sharedBuild)
        {
            BuildPlanItem item = items[target];
            if (item.Action == BuildAction.BuildImage)
            {
                continue;
            }

            items[target] = item with
            {
                Action = BuildAction.BuildImage,
                Reasons =
                [
                    ..item.Reasons,
                    new(
                        $"Equivalent target '{GetName(invalidatedItem.Target)}' must build.",
                        GetCause(invalidatedItem))
                ]
            };
        }
    }

    private static void UsePublishedParentsForBuilds(
        BuildGraph graph,
        IDictionary<BuildTarget, BuildPlanItem> items,
        IReadOnlyDictionary<BuildTarget, PublishedImage> publishedImages)
    {
        foreach (BuildPlanItem childItem in items.Values
            .Where(item => item.Action == BuildAction.BuildImage)
            .ToArray())
        {
            foreach (BuildTarget parent in graph.Parents[childItem.Target])
            {
                BuildPlanItem parentItem = items[parent];
                BuildReason reason = new(
                    $"The image is required by '{GetName(childItem.Target)}'.",
                    GetCause(childItem));

                if (parentItem.Action == BuildAction.NoAction)
                {
                    if (!publishedImages.ContainsKey(parent))
                    {
                        throw new InvalidOperationException(
                            $"Required dependency '{GetName(parent)}' has no published image.");
                    }

                    items[parent] = parentItem with
                    {
                        Action = BuildAction.UsePublishedImage,
                        Reasons = [reason, ..parentItem.Reasons]
                    };
                }
                else if (!parentItem.Reasons.Contains(reason))
                {
                    items[parent] = parentItem with
                    {
                        Reasons = [reason, ..parentItem.Reasons]
                    };
                }
            }
        }
    }

    private static BuildPlanItem CreateItem(
        BuildTarget target,
        BuildAction action,
        IEnumerable<BuildReason> reasons,
        PublishedImage? publishedImage)
    {
        if ((action is BuildAction.UsePublishedImage or BuildAction.PublishExistingImage) &&
            publishedImage is null)
        {
            throw new InvalidOperationException(
                $"Planning selected '{action}' for '{GetName(target)}' " +
                "without a published image.");
        }

        return new(target, action, reasons.ToArray(), publishedImage);
    }

    private void LogPlan(IEnumerable<BuildPlanItem> plan)
    {
        foreach (BuildPlanItem item in plan)
        {
            _logger.LogInformation(
                "Build plan for {DockerfilePath}: {Action}. {Reasons}",
                GetName(item.Target),
                item.Action,
                string.Join(" ", item.Reasons.Select(FormatReason)));
        }
    }

    private static BuildReason GetCause(BuildPlanItem item) => item.Reasons.Last();

    private static string FormatReason(BuildReason reason) =>
        reason.Cause is null
            ? reason.Message
            : $"{reason.Message} {FormatReason(reason.Cause)}";

    private static string GetName(BuildTarget target) =>
        $"{target.Repo.Name} ({target.Platform.DockerfilePathRelativeToManifest})";
}
