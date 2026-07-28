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
    /// Gets the build decisions in execution order: every platform comes after the platforms it depends on.
    /// </summary>
    public static IReadOnlyList<PlannedPlatform> GetDecisionsInBuildOrder(this BuildPlan plan)
    {
        HashSet<BuildPlanNode> visited = [];
        List<PlannedPlatform> decisions = [];

        foreach (BuildPlanNode root in plan.Roots)
        {
            Visit(root);
        }

        return decisions;

        void Visit(BuildPlanNode node)
        {
            if (!visited.Add(node))
            {
                return;
            }

            foreach (BuildPlanNode dependency in node.Dependencies)
            {
                Visit(dependency);
            }

            if (node.Decision is not null)
            {
                decisions.Add(node.Decision);
            }
        }
    }

    /// <summary>Gets whether any platform will reuse previously published metadata.</summary>
    public static bool HasReusablePlatforms(this BuildPlan plan) =>
        plan.GetDecisionsInBuildOrder().Any(planned => planned.Action is
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
        IReadOnlyDictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> dependentsByNode =
            GetDependentsByNode(nodes);
        IReadOnlyDictionary<PlatformInfo, BuildPlanNode> nodeByPlatform =
            nodes.ToDictionary(node => node.Platform);
        HashSet<PlatformInfo> addedPlatforms = [];
        List<PlatformInfo> platforms = [];
        IEnumerable<PlatformInfo> origins = plan
            .GetDecisionsInBuildOrder()
            .Where(planned => planned.Action == BuildAction.Build)
            .SelectMany(planned => planned.Causes)
            .Where(cause => cause.IsDirect() && reasonSet.Contains(cause.Reason))
            .Select(cause => cause.Origin)
            .Distinct();

        foreach (PlatformInfo origin in origins)
        {
            platforms.AddRange(
                GetConnectedNodes(nodeByPlatform[origin], dependentsByNode)
                    .Select(node => node.Platform)
                    .Where(addedPlatforms.Add));
        }

        return platforms;
    }

    private static IReadOnlyList<BuildPlanNode> GetNodes(BuildPlan plan)
    {
        HashSet<BuildPlanNode> visited = [];
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
            foreach (BuildPlanNode dependency in node.Dependencies)
            {
                Visit(dependency);
            }
        }
    }

    private static IReadOnlyDictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> GetDependentsByNode(
        IReadOnlyList<BuildPlanNode> nodes)
    {
        Dictionary<BuildPlanNode, List<BuildPlanNode>> dependentsByNode =
            nodes.ToDictionary(node => node, _ => new List<BuildPlanNode>());

        foreach (BuildPlanNode node in nodes)
        {
            foreach (BuildPlanNode dependency in node.Dependencies)
            {
                dependentsByNode[dependency].Add(node);
            }
        }

        return dependentsByNode.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<BuildPlanNode>)pair.Value.ToArray());
    }

    private static IReadOnlyList<BuildPlanNode> GetConnectedNodes(
        BuildPlanNode node,
        IReadOnlyDictionary<BuildPlanNode, IReadOnlyList<BuildPlanNode>> dependentsByNode)
    {
        HashSet<BuildPlanNode> visited = [];
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
            foreach (BuildPlanNode related in dependentsByNode[current].Concat(current.Dependencies))
            {
                Visit(related);
            }
        }
    }
}
