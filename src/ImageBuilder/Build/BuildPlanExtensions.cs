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
    /// <summary>Gets platforms with build decisions in dependency order.</summary>
    public static IReadOnlyList<PlannedPlatform> GetPlatformsWithDecisionsInBuildOrder(this BuildPlan plan)
    {
        IReadOnlyList<PlannedPlatform> allPlatforms = GetAllPlatforms(plan);
        IReadOnlyDictionary<PlannedPlatform, List<PlannedPlatform>> dependenciesByPlatform =
            GetDependenciesByPlatform(allPlatforms);
        Dictionary<PlannedPlatform, int> depthByPlatform = new(ReferenceEqualityComparer.Instance);

        return [..allPlatforms
            .Where(platform => platform.Action is not null)
            .OrderBy(GetDependencyDepth)];

        int GetDependencyDepth(PlannedPlatform platform)
        {
            if (!depthByPlatform.TryGetValue(platform, out int depth))
            {
                IReadOnlyList<PlannedPlatform> dependencies = dependenciesByPlatform[platform];
                depth = dependencies.Count == 0 ? 0 : dependencies.Max(GetDependencyDepth) + 1;
                depthByPlatform.Add(platform, depth);
            }

            return depth;
        }
    }

    /// <summary>Gets whether any platform will reuse previously published metadata.</summary>
    public static bool HasReusablePlatforms(this BuildPlan plan) =>
        plan.GetPlatformsWithDecisionsInBuildOrder().Any(planned => planned.Action is
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
        IReadOnlyList<PlannedPlatform> allPlatforms = GetAllPlatforms(plan);
        IReadOnlyDictionary<PlannedPlatform, List<PlannedPlatform>> dependenciesByPlatform =
            GetDependenciesByPlatform(allPlatforms);

        IReadOnlyDictionary<PlatformInfo, PlannedPlatform> plannedByPlatform =
            allPlatforms.ToDictionary(platform => platform.Platform);
        HashSet<PlatformInfo> addedPlatforms = [];
        List<PlatformInfo> platforms = [];
        IEnumerable<PlatformInfo> buildOrigins = allPlatforms
            .Where(planned => planned.Action == BuildAction.Build)
            .SelectMany(planned => planned.Causes)
            .Where(cause => cause.IsDirect() && reasonSet.Contains(cause.Reason))
            .Select(cause => cause.Origin)
            .Distinct();

        foreach (PlatformInfo buildOrigin in buildOrigins)
        {
            VisitConnected(plannedByPlatform[buildOrigin]);
        }

        return platforms;

        void VisitConnected(PlannedPlatform platform)
        {
            if (!addedPlatforms.Add(platform.Platform))
            {
                return;
            }

            platforms.Add(platform.Platform);
            foreach (PlannedPlatform related in dependenciesByPlatform[platform].Concat(platform.Dependents))
            {
                VisitConnected(related);
            }
        }
    }

    private static IReadOnlyDictionary<PlannedPlatform, List<PlannedPlatform>> GetDependenciesByPlatform(
        IReadOnlyList<PlannedPlatform> platforms)
    {
        Dictionary<PlannedPlatform, List<PlannedPlatform>> dependenciesByPlatform =
            new(ReferenceEqualityComparer.Instance);

        foreach (PlannedPlatform platform in platforms)
        {
            dependenciesByPlatform.Add(platform, []);
        }

        foreach (PlannedPlatform platform in platforms)
        {
            foreach (PlannedPlatform dependent in platform.Dependents)
            {
                dependenciesByPlatform[dependent].Add(platform);
            }
        }

        return dependenciesByPlatform;
    }

    private static IReadOnlyList<PlannedPlatform> GetAllPlatforms(BuildPlan plan)
    {
        HashSet<PlannedPlatform> visited = new(ReferenceEqualityComparer.Instance);
        List<PlannedPlatform> platforms = [];

        foreach (PlannedPlatform root in plan.Roots)
        {
            Visit(root);
        }

        return platforms;

        void Visit(PlannedPlatform platform)
        {
            if (!visited.Add(platform))
            {
                return;
            }

            platforms.Add(platform);
            foreach (PlannedPlatform dependent in platform.Dependents)
            {
                Visit(dependent);
            }
        }
    }
}
