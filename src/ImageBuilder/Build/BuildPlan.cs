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
    private readonly IReadOnlyDictionary<PlatformInfo, PlannedPlatform> _decisionsByPlatform;

    /// <summary>
    /// Creates a plan from the decisions for selected platforms and the relationships between all platforms.
    /// </summary>
    public BuildPlan(IEnumerable<PlannedPlatform> decisions, PlatformDependencyGraph dependencyGraph)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        _dependencyGraph = dependencyGraph ?? throw new ArgumentNullException(nameof(dependencyGraph));
        _decisionsByPlatform = decisions.ToDictionary(planned => planned.Platform);
        DecisionsInBuildOrder =
            [.._decisionsByPlatform.Values.OrderBy(planned => dependencyGraph.GetDependencyDepth(planned.Platform))];
    }

    /// <summary>
    /// Gets every platform in the plan, including platforms that connect selected platforms but do not have a build
    /// decision of their own.
    /// </summary>
    public IReadOnlyCollection<PlatformInfo> Platforms => _dependencyGraph.Platforms;

    /// <summary>
    /// Gets the build decisions in execution order: every platform comes after the platforms it depends on.
    /// </summary>
    public IReadOnlyList<PlannedPlatform> DecisionsInBuildOrder { get; }

    /// <summary>Gets whether any platform will reuse previously published metadata.</summary>
    public bool HasReusablePlatforms =>
        DecisionsInBuildOrder.Any(planned => planned.Action is
            BuildAction.Reuse or BuildAction.ReuseAndPublishTags);

    /// <summary>
    /// Gets the build decision for a platform, or null when that platform was not selected for planning.
    /// </summary>
    public PlannedPlatform? GetDecision(PlatformInfo platform) =>
        _decisionsByPlatform.GetValueOrDefault(platform);

    /// <summary>Gets the platforms that the given platform directly depends on.</summary>
    public IReadOnlyList<PlatformInfo> GetDependencies(PlatformInfo platform) =>
        _dependencyGraph.GetParents(platform);

    /// <summary>Gets the platforms that directly depend on the given platform.</summary>
    public IReadOnlyList<PlatformInfo> GetDependents(PlatformInfo platform) =>
        _dependencyGraph.GetChildren(platform);

    /// <summary>
    /// Gets every platform connected to a planned build.
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
        IEnumerable<PlatformInfo> origins = DecisionsInBuildOrder
            .Where(planned => planned.Action == BuildAction.Build)
            .SelectMany(planned => planned.Causes)
            .Where(cause =>
                cause.IsDirect() &&
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
