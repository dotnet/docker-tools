// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Describes how a platform should be handled by build execution.
/// </summary>
public enum BuildDisposition
{
    /// <summary>The platform is outside the selected planning policy.</summary>
    Skip,

    /// <summary>The platform must be built.</summary>
    Build,

    /// <summary>The previously published platform can be reused without changes.</summary>
    Reuse,

    /// <summary>The previously published image can be reused but must be published for this platform.</summary>
    ReuseAndPublish
}

/// <summary>
/// Identifies a condition that affected a platform's build disposition.
/// </summary>
public enum BuildPlanReason
{
    CacheDisabled,
    MissingImageInfo,
    BaseImageChanged,
    DockerfileChanged,
    MissingTags,
    EquivalentBuildChanged
}

/// <summary>
/// Explains a condition that caused a platform to receive its build disposition.
/// </summary>
/// <param name="Reason">The condition that initiated the decision.</param>
/// <param name="Origin">The platform where the condition was observed.</param>
/// <param name="DependencyPath">
/// The dependency path from <paramref name="Origin"/> to the affected platform, including both.
/// </param>
public sealed record BuildCause(
    BuildPlanReason Reason,
    PlatformInfo Origin,
    IReadOnlyList<PlatformInfo> DependencyPath);

/// <summary>
/// The planned build disposition and supporting data for one platform.
/// </summary>
/// <param name="Platform">The platform being planned.</param>
/// <param name="Disposition">How the platform should be handled.</param>
/// <param name="CachedPlatform">Previously published metadata to reuse, when available.</param>
/// <param name="Causes">Conditions that produced the disposition.</param>
public sealed record BuildPlanEntry(
    PlatformInfo Platform,
    BuildDisposition Disposition,
    PlatformData? CachedPlatform,
    IReadOnlyList<BuildCause> Causes);

/// <summary>
/// The dependency edges between a set of planned platforms, materialized so that scheduling
/// questions can be answered without consulting the manifest.
/// </summary>
public sealed class PlatformDependencyGraph
{
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _children;
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _parents;

    private PlatformDependencyGraph(
        IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> children,
        IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> parents)
    {
        _children = children;
        _parents = parents;
    }

    /// <summary>
    /// Creates the graph for the given platforms, resolving each platform's parents once and
    /// inverting them to get its children.
    /// </summary>
    public static PlatformDependencyGraph Create(
        ManifestInfo manifest,
        IReadOnlyCollection<PlatformInfo> platforms)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(platforms);

        Dictionary<PlatformInfo, PlatformInfo[]> parents = platforms.ToDictionary(
            platform => platform,
            platform => manifest.GetParents(platform, platforms).ToArray());
        Dictionary<PlatformInfo, List<PlatformInfo>> children =
            platforms.ToDictionary(platform => platform, _ => new List<PlatformInfo>());

        foreach (PlatformInfo platform in platforms)
        {
            foreach (PlatformInfo parent in parents[platform])
            {
                children[parent].Add(platform);
            }
        }

        return new PlatformDependencyGraph(
            children.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            parents);
    }

    /// <summary>Gets the platforms that directly depend on the given platform.</summary>
    public IReadOnlyList<PlatformInfo> GetChildren(PlatformInfo platform) => _children[platform];

    /// <summary>
    /// Gets the platforms reachable from the given platform through dependency edges in either
    /// direction, starting with the platform itself. Because a build affects everything connected to
    /// it, these platforms must be scheduled together.
    /// </summary>
    public IReadOnlyList<PlatformInfo> GetConnectedPlatforms(PlatformInfo platform)
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
            foreach (PlatformInfo related in _children[current].Concat(_parents[current]))
            {
                Visit(related);
            }
        }
    }
}

/// <summary>
/// Immutable decisions for a set of manifest platforms.
/// </summary>
public sealed class BuildPlan
{
    private readonly IReadOnlyDictionary<PlatformInfo, BuildPlanEntry> _entriesByPlatform;
    private readonly PlatformDependencyGraph _dependencyGraph;

    /// <summary>
    /// Creates a plan from decisions for unique manifest platforms and the dependency edges
    /// between them.
    /// </summary>
    public BuildPlan(IEnumerable<BuildPlanEntry> entries, PlatformDependencyGraph dependencyGraph)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _dependencyGraph = dependencyGraph ?? throw new ArgumentNullException(nameof(dependencyGraph));
        Entries = entries.ToArray();
        _entriesByPlatform = Entries.ToDictionary(entry => entry.Platform);
    }

    /// <summary>Gets the platform decisions in planning order.</summary>
    public IReadOnlyList<BuildPlanEntry> Entries { get; }

    /// <summary>Gets whether any platform will reuse previously published metadata.</summary>
    public bool HasReusablePlatforms =>
        Entries.Any(entry => entry.Disposition is
            BuildDisposition.Reuse or BuildDisposition.ReuseAndPublish);

    /// <summary>Gets the decision for a platform included in this plan.</summary>
    public BuildPlanEntry GetEntry(PlatformInfo platform) =>
        _entriesByPlatform.TryGetValue(platform, out BuildPlanEntry? entry) ?
            entry :
            throw new InvalidOperationException(
                $"Platform '{platform.DockerfilePathRelativeToManifest}' is not part of this build plan.");

    /// <summary>
    /// Gets platforms that must be scheduled to execute planned builds, including their ancestors.
    /// </summary>
    public IReadOnlyCollection<PlatformInfo> GetPlatformsToSchedule() =>
        GetPlatformsToSchedule(Enum.GetValues<BuildPlanReason>());

    /// <summary>
    /// Gets platforms required to execute builds caused by any of the specified reasons.
    /// </summary>
    public IReadOnlyCollection<PlatformInfo> GetPlatformsToSchedule(IEnumerable<BuildPlanReason> reasons)
    {
        HashSet<BuildPlanReason> reasonSet = reasons.ToHashSet();
        HashSet<PlatformInfo> addedPlatforms = [];
        List<PlatformInfo> platforms = [];
        IEnumerable<PlatformInfo> origins = Entries
            .Where(entry => entry.Disposition == BuildDisposition.Build)
            .SelectMany(entry => entry.Causes)
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
