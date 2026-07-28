// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>Provides queries over a build plan.</summary>
public static class BuildPlanExtensions
{
    /// <summary>
    /// Gets the nodes with build decisions in execution order.
    /// </summary>
    public static IReadOnlyList<BuildPlanNode> GetNodesWithDecisionsInBuildOrder(this BuildPlan plan)
    {
        IReadOnlyList<BuildPlanNode> nodes = GetNodes(plan);
        IReadOnlyDictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> dependenciesByNode =
            GetDependenciesByNode(nodes);
        Dictionary<BuildPlanNode, int> depthByNode = new(ReferenceEqualityComparer.Instance);

        return [..nodes
            .Where(node => node.Decision is not null)
            .OrderBy(GetDependencyDepth)];

        int GetDependencyDepth(BuildPlanNode node)
        {
            if (!depthByNode.TryGetValue(node, out int depth))
            {
                IReadOnlyList<BuildPlanNode> dependencies = dependenciesByNode[node];
                depth = dependencies.Count == 0 ? 0 : dependencies.Max(GetDependencyDepth) + 1;
                depthByNode.Add(node, depth);
            }

            return depth;
        }
    }

    /// <summary>Gets whether any platform will reuse previously published metadata.</summary>
    public static bool HasReusablePlatforms(this BuildPlan plan) =>
        plan.GetNodesWithDecisionsInBuildOrder().Any(node => node.Decision?.Action is
            BuildAction.Reuse or BuildAction.ReuseAndPublishTags);

    /// <summary>Gets every platform connected to a planned build.</summary>
    public static IReadOnlyCollection<PlatformInfo> GetPlatformsToSchedule(this BuildPlan plan) =>
        plan.GetPlatformsToSchedule(Enum.GetValues<BuildPlanReason>());

    /// <summary>
    /// Gets platforms required to execute builds caused by any of the specified reasons, including everything connected
    /// to them.
    /// </summary>
    /// <remarks>
    /// Filtering by reason lets a caller act on only the changes it is responsible for. A reason that is left out does
    /// not suppress a build; it just does not, by itself, cause one.
    /// </remarks>
    public static IReadOnlyCollection<PlatformInfo> GetPlatformsToSchedule(
        this BuildPlan plan,
        IEnumerable<BuildPlanReason> reasons)
    {
        HashSet<BuildPlanReason> reasonSet = reasons.ToHashSet();
        IReadOnlyList<BuildPlanNode> nodes = GetNodes(plan);
        IReadOnlyDictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> dependenciesByNode =
            GetDependenciesByNode(nodes);
        IReadOnlyDictionary<PlatformInfo, BuildPlanNode> nodeByPlatform =
            nodes.ToDictionary(node => node.Platform);
        HashSet<PlatformInfo> addedPlatforms = [];
        List<PlatformInfo> platforms = [];
        IEnumerable<PlatformInfo> origins = plan
            .GetNodesWithDecisionsInBuildOrder()
            .Select(node => node.Decision)
            .OfType<BuildDecision>()
            .Where(decision => decision.Action == BuildAction.Build)
            .SelectMany(decision => decision.Causes)
            .Where(cause => cause.IsDirect() && reasonSet.Contains(cause.Reason))
            .Select(cause => cause.Origin)
            .Distinct();

        foreach (PlatformInfo origin in origins)
        {
            platforms.AddRange(
                GetConnectedNodes(nodeByPlatform[origin], dependenciesByNode)
                    .Select(node => node.Platform)
                    .Where(addedPlatforms.Add));
        }

        return platforms;
    }

    private static IReadOnlyList<BuildPlanNode> GetNodes(BuildPlan plan)
    {
        HashSet<BuildPlanNode> visited = new(ReferenceEqualityComparer.Instance);
        List<BuildPlanNode> nodes = [];
        foreach (BuildPlanNode root in plan.Roots)
        {
            Visit(root);
        }

        return nodes;

        void Visit(BuildPlanNode node)
        {
            if (!visited.Add(node))
            {
                return;
            }

            nodes.Add(node);
            foreach (BuildPlanNode dependent in node.Dependents)
            {
                Visit(dependent);
            }
        }
    }

    private static IReadOnlyDictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> GetDependenciesByNode(
        IReadOnlyList<BuildPlanNode> nodes)
    {
        Dictionary<BuildPlanNode, List<BuildPlanNode>> dependenciesByNode =
            new(ReferenceEqualityComparer.Instance);

        foreach (BuildPlanNode node in nodes)
        {
            dependenciesByNode.Add(node, []);
        }

        foreach (BuildPlanNode node in nodes)
        {
            foreach (BuildPlanNode dependent in node.Dependents)
            {
                dependenciesByNode[dependent].Add(node);
            }
        }

        Dictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> result =
            new(ReferenceEqualityComparer.Instance);

        foreach ((BuildPlanNode node, List<BuildPlanNode> dependencies) in dependenciesByNode)
        {
            result.Add(node, dependencies);
        }

        return result;
    }

    private static IReadOnlyList<BuildPlanNode> GetConnectedNodes(
        BuildPlanNode node,
        IReadOnlyDictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> dependenciesByNode)
    {
        HashSet<BuildPlanNode> visited = new(ReferenceEqualityComparer.Instance);
        List<BuildPlanNode> connected = [];
        Visit(node);
        return connected;

        void Visit(BuildPlanNode current)
        {
            if (!visited.Add(current))
            {
                return;
            }

            connected.Add(current);
            foreach (BuildPlanNode related in dependenciesByNode[current].Concat(current.Dependents))
            {
                Visit(related);
            }
        }
    }
}
