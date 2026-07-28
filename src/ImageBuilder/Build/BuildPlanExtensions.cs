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
        IReadOnlyDictionary<PlatformInfo, int> dependencyDepths = plan.GetDependencyDepths();
        return [..plan.DecisionsByPlatform.Values.OrderBy(decision => dependencyDepths[decision.Platform])];
    }

    /// <summary>Gets whether any platform will reuse previously published metadata.</summary>
    public static bool HasReusablePlatforms(this BuildPlan plan) =>
        plan.DecisionsByPlatform.Values.Any(planned => planned.Action is
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
        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependentsByPlatform =
            plan.GetDependentsByPlatform();
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
            platforms.AddRange(GetConnectedPlatforms(plan, dependentsByPlatform, origin).Where(addedPlatforms.Add));
        }

        return platforms;
    }

    internal static IReadOnlyDictionary<PlatformInfo, int> GetDependencyDepths(this BuildPlan plan)
    {
        Dictionary<PlatformInfo, int> dependencyDepths = [];

        foreach (PlatformInfo platform in plan.DependenciesByPlatform.Keys)
        {
            GetDependencyDepth(platform);
        }

        return dependencyDepths;

        int GetDependencyDepth(PlatformInfo platform)
        {
            if (!dependencyDepths.TryGetValue(platform, out int depth))
            {
                IReadOnlyList<PlatformInfo> dependencies = plan.DependenciesByPlatform[platform];
                depth = dependencies.Count == 0 ? 0 : dependencies.Max(GetDependencyDepth) + 1;
                dependencyDepths[platform] = depth;
            }

            return depth;
        }
    }

    internal static IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> GetDependentsByPlatform(
        this BuildPlan plan)
    {
        Dictionary<PlatformInfo, List<PlatformInfo>> dependentsByPlatform =
            plan.DependenciesByPlatform.Keys.ToDictionary(platform => platform, _ => new List<PlatformInfo>());

        foreach ((PlatformInfo platform, IReadOnlyList<PlatformInfo> dependencies) in plan.DependenciesByPlatform)
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

    private static IReadOnlyList<PlatformInfo> GetConnectedPlatforms(
        BuildPlan plan,
        IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> dependentsByPlatform,
        PlatformInfo platform)
    {
        HashSet<PlatformInfo> visited = [];
        List<PlatformInfo> connected = [];
        Visit(platform);
        return connected;

        void Visit(PlatformInfo current)
        {
            if (!visited.Add(current))
            {
                return;
            }

            connected.Add(current);
            foreach (PlatformInfo related in dependentsByPlatform[current].Concat(plan.DependenciesByPlatform[current]))
            {
                Visit(related);
            }
        }
    }
}
