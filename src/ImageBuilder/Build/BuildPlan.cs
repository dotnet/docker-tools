// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Platforms, their dependencies, and the build decision for each selected platform.
/// </summary>
public sealed class BuildPlan
{
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _dependencies;
    private readonly IReadOnlyDictionary<PlatformInfo, PlannedPlatform> _decisionsByPlatform;
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _dependents;
    private readonly IReadOnlyDictionary<PlatformInfo, int> _dependencyDepths;
    private readonly IReadOnlyCollection<PlatformInfo> _platforms;

    /// <summary>
    /// Creates a plan containing all given platforms and build decisions for the selected platforms.
    /// </summary>
    /// <param name="manifest">Manifest that defines the platform dependencies.</param>
    /// <param name="platforms">Every platform needed to determine build order.</param>
    /// <param name="decisions">Build decisions for the selected platforms.</param>
    public BuildPlan(
        ManifestInfo manifest,
        IReadOnlyCollection<PlatformInfo> platforms,
        IEnumerable<PlannedPlatform> decisions)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(platforms);
        ArgumentNullException.ThrowIfNull(decisions);

        _dependencies = platforms.ToDictionary(
            platform => platform,
            platform => manifest.GetParents(platform, platforms).ToArray());

        Dictionary<PlatformInfo, List<PlatformInfo>> dependents =
            platforms.ToDictionary(platform => platform, _ => new List<PlatformInfo>());

        foreach (PlatformInfo platform in platforms)
        {
            foreach (PlatformInfo dependency in _dependencies[platform])
            {
                dependents[dependency].Add(platform);
            }
        }

        _dependents = dependents.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        Dictionary<PlatformInfo, int> dependencyDepths = [];
        foreach (PlatformInfo platform in platforms)
        {
            GetDependencyDepth(platform);
        }

        _dependencyDepths = dependencyDepths;
        _platforms = _dependencies.Keys.ToArray();
        _decisionsByPlatform = decisions.ToDictionary(planned => planned.Platform);

        if (_decisionsByPlatform.Keys.Any(platform => !_dependencies.ContainsKey(platform)))
        {
            throw new ArgumentException("Build decisions must belong to a platform in the plan.", nameof(decisions));
        }

        DecisionsInBuildOrder =
            [.._decisionsByPlatform.Values.OrderBy(decision => _dependencyDepths[decision.Platform])];

        int GetDependencyDepth(PlatformInfo platform)
        {
            if (!dependencyDepths.TryGetValue(platform, out int depth))
            {
                depth = _dependencies[platform].Length == 0 ? 0 : _dependencies[platform].Max(GetDependencyDepth) + 1;
                dependencyDepths[platform] = depth;
            }

            return depth;
        }
    }

    private BuildPlan(BuildPlan graph, IEnumerable<PlannedPlatform> decisions)
    {
        _dependencies = graph._dependencies;
        _dependents = graph._dependents;
        _dependencyDepths = graph._dependencyDepths;
        _platforms = graph._platforms;
        _decisionsByPlatform = decisions.ToDictionary(planned => planned.Platform);
        DecisionsInBuildOrder =
            [.._decisionsByPlatform.Values.OrderBy(decision => _dependencyDepths[decision.Platform])];
    }

    /// <summary>Creates the platform graph before build decisions have been made.</summary>
    internal BuildPlan(ManifestInfo manifest, IReadOnlyCollection<PlatformInfo> platforms)
        : this(manifest, platforms, [])
    {
    }

    /// <summary>Returns this platform graph with its completed build decisions.</summary>
    internal BuildPlan WithDecisions(IEnumerable<PlannedPlatform> decisions) =>
        new(this, decisions);

    /// <summary>
    /// Gets every platform in the plan, including platforms that connect selected platforms but do not have a build
    /// decision of their own.
    /// </summary>
    public IReadOnlyCollection<PlatformInfo> Platforms => _platforms;

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
        _dependencies[platform];

    /// <summary>Gets the platforms that directly depend on the given platform.</summary>
    public IReadOnlyList<PlatformInfo> GetDependents(PlatformInfo platform) =>
        _dependents[platform];

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
            platforms.AddRange(GetConnectedPlatforms(origin).Where(addedPlatforms.Add));
        }

        return platforms;
    }

    internal int GetDependencyDepth(PlatformInfo platform) => _dependencyDepths[platform];

    private IReadOnlyList<PlatformInfo> GetConnectedPlatforms(PlatformInfo platform)
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
            foreach (PlatformInfo related in _dependents[current].Concat(_dependencies[current]))
            {
                Visit(related);
            }
        }
    }
}
