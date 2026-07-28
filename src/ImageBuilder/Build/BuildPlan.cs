// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Immutable decisions for a set of manifest platforms.
/// </summary>
public sealed class BuildPlan
{
    private readonly PlatformDependencyGraph _dependencyGraph;

    /// <summary>
    /// Creates a plan from decisions for unique manifest platforms and the dependency edges
    /// between them.
    /// </summary>
    public BuildPlan(IEnumerable<PlannedPlatform> platforms, PlatformDependencyGraph dependencyGraph)
    {
        ArgumentNullException.ThrowIfNull(platforms);
        _dependencyGraph = dependencyGraph ?? throw new ArgumentNullException(nameof(dependencyGraph));
        Platforms =
            [..platforms.OrderBy(planned => dependencyGraph.GetDependencyDepth(planned.Platform))];
    }

    /// <summary>
    /// Gets the planned platforms in dependency order: every platform comes after the platforms it
    /// depends on.
    /// </summary>
    public IReadOnlyList<PlannedPlatform> Platforms { get; }

    /// <summary>Gets whether any platform will reuse previously published metadata.</summary>
    public bool HasReusablePlatforms =>
        Platforms.Any(planned => planned.Action is
            BuildAction.Reuse or BuildAction.ReuseAndPublishTags);

    /// <summary>
    /// Gets platforms that must be scheduled to execute planned builds, including their ancestors.
    /// </summary>
    public IReadOnlyCollection<PlatformInfo> GetPlatformsToSchedule() =>
        GetPlatformsToSchedule(Enum.GetValues<BuildPlanReason>());

    /// <summary>
    /// Gets platforms required to execute builds caused by any of the specified reasons, including
    /// everything connected to them.
    /// </summary>
    /// <remarks>
    /// Filtering by reason lets a caller act on only the changes it is responsible for. A reason
    /// that is left out does not suppress a build; it just does not, by itself, cause one.
    /// </remarks>
    public IReadOnlyCollection<PlatformInfo> GetPlatformsToSchedule(IEnumerable<BuildPlanReason> reasons)
    {
        HashSet<BuildPlanReason> reasonSet = reasons.ToHashSet();
        HashSet<PlatformInfo> addedPlatforms = [];
        List<PlatformInfo> platforms = [];
        IEnumerable<PlatformInfo> origins = Platforms
            .Where(planned => planned.Action == BuildAction.Build)
            .SelectMany(planned => planned.Causes)
            .Where(cause =>
                cause.DependencyPath.Count == 1 &&
                reasonSet.Contains(cause.Reason))
            .Select(cause => cause.Origin)
            .Distinct();

        foreach (PlatformInfo origin in origins)
        {
            platforms.AddRange(
                _dependencyGraph.GetConnectedPlatforms(origin).Where(addedPlatforms.Add));
        }

        return platforms;
    }
}
