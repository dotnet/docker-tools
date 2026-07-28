// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// The dependency relationships between manifest platforms.
/// </summary>
public sealed class PlatformDependencyGraph
{
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _dependencies;
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _dependents;

    private PlatformDependencyGraph(
        IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> dependencies,
        IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> dependents,
        IReadOnlyList<PlatformInfo> platformsInDependencyOrder)
    {
        _dependencies = dependencies;
        _dependents = dependents;
        PlatformsInDependencyOrder = platformsInDependencyOrder;
    }

    /// <summary>
    /// Gets every platform in dependency order, with each platform after everything it depends on.
    /// </summary>
    public IReadOnlyList<PlatformInfo> PlatformsInDependencyOrder { get; }

    /// <summary>Creates the dependency graph for the given platforms.</summary>
    public static PlatformDependencyGraph Create(
        ManifestInfo manifest,
        IReadOnlyCollection<PlatformInfo> platforms)
    {
        Dictionary<PlatformInfo, PlatformInfo[]> dependencies = platforms.ToDictionary(
            platform => platform,
            platform => manifest.GetParents(platform, platforms).ToArray());

        Dictionary<PlatformInfo, List<PlatformInfo>> dependents =
            platforms.ToDictionary(platform => platform, _ => new List<PlatformInfo>());

        foreach (PlatformInfo platform in platforms)
        {
            foreach (PlatformInfo dependency in dependencies[platform])
            {
                dependents[dependency].Add(platform);
            }
        }

        HashSet<PlatformInfo> visited = [];
        HashSet<PlatformInfo> visiting = [];
        List<PlatformInfo> platformsInDependencyOrder = [];

        foreach (PlatformInfo platform in platforms)
        {
            VisitDependencies(platform);
        }

        return new PlatformDependencyGraph(
            dependencies,
            dependents.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            platformsInDependencyOrder);

        void VisitDependencies(PlatformInfo platform)
        {
            if (visited.Contains(platform))
            {
                return;
            }

            if (!visiting.Add(platform))
            {
                throw new InvalidOperationException(
                    $"Platform dependency cycle detected at '{platform.DockerfilePathRelativeToManifest}'.");
            }

            foreach (PlatformInfo dependency in dependencies[platform])
            {
                VisitDependencies(dependency);
            }

            visiting.Remove(platform);
            visited.Add(platform);
            platformsInDependencyOrder.Add(platform);
        }
    }

    /// <summary>Gets the platforms that the given platform directly depends on.</summary>
    public IReadOnlyList<PlatformInfo> GetDependencies(PlatformInfo platform) => _dependencies[platform];

    /// <summary>Gets the platforms that directly depend on the given platform.</summary>
    public IReadOnlyList<PlatformInfo> GetDependents(PlatformInfo platform) => _dependents[platform];

    /// <summary>
    /// Creates a build plan by attaching the given decisions to this graph. Platforms without decisions remain in the
    /// graph so dependencies can still be traversed through them.
    /// </summary>
    public BuildPlan CreateBuildPlan(IEnumerable<PlannedPlatform> plannedPlatforms)
    {
        IReadOnlyDictionary<PlatformInfo, PlannedPlatform> plannedByPlatform =
            plannedPlatforms.ToDictionary(planned => planned.Platform);
        Dictionary<PlatformInfo, PlannedPlatform> nodesByPlatform = [];

        PlannedPlatform CreateNode(PlatformInfo platform)
        {
            if (!nodesByPlatform.TryGetValue(platform, out PlannedPlatform? node))
            {
                PlannedPlatform? decision = plannedByPlatform.GetValueOrDefault(platform);
                node = new PlannedPlatform(
                    Platform: platform,
                    Action: decision?.Action,
                    ImageToReuse: decision?.ImageToReuse,
                    Causes: decision?.Causes ?? [],
                    Dependents: GetDependents(platform).Select(CreateNode).ToArray());
                nodesByPlatform.Add(platform, node);
            }

            return node;
        }

        PlannedPlatform[] roots = PlatformsInDependencyOrder
            .Where(platform => GetDependencies(platform).Count == 0)
            .Select(CreateNode)
            .ToArray();

        return new BuildPlan(roots);
    }

}
