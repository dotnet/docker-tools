// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// The dependency edges between a set of manifest platforms, materialized so that scheduling questions can be
/// answered without consulting the manifest.
/// </summary>
public sealed class PlatformDependencyGraph
{
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _children;
    private readonly IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> _parents;
    private readonly IReadOnlyDictionary<PlatformInfo, int> _depths;
    private readonly IReadOnlyCollection<PlatformInfo> _platforms;

    private PlatformDependencyGraph(
        IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> children,
        IReadOnlyDictionary<PlatformInfo, PlatformInfo[]> parents,
        IReadOnlyDictionary<PlatformInfo, int> depths)
    {
        _children = children;
        _parents = parents;
        _depths = depths;
        _platforms = parents.Keys.ToArray();
    }

    /// <summary>Gets every platform in the graph.</summary>
    public IReadOnlyCollection<PlatformInfo> Platforms => _platforms;

    /// <summary>
    /// Creates the graph for the given platforms, resolving each platform's parents once and
    /// inverting them to get its children.
    /// </summary>
    public static PlatformDependencyGraph Create(ManifestInfo manifest, IReadOnlyCollection<PlatformInfo> platforms)
    {
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

        Dictionary<PlatformInfo, int> depths = [];

        foreach (PlatformInfo platform in platforms)
        {
            GetDepth(platform);
        }

        return new PlatformDependencyGraph(
            children.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            parents,
            depths);

        int GetDepth(PlatformInfo platform)
        {
            if (!depths.TryGetValue(platform, out int depth))
            {
                depth = parents[platform].Length == 0 ? 0 : parents[platform].Max(GetDepth) + 1;
                depths[platform] = depth;
            }

            return depth;
        }
    }

    /// <summary>Gets the platforms that directly depend on the given platform.</summary>
    public IReadOnlyList<PlatformInfo> GetChildren(PlatformInfo platform) => _children[platform];

    /// <summary>Gets the platforms that the given platform directly depends on.</summary>
    public IReadOnlyList<PlatformInfo> GetParents(PlatformInfo platform) => _parents[platform];

    /// <summary>
    /// Gets the number of dependency edges between the platform and the furthest platform it
    /// depends on. Because a platform's depth is always greater than its parents' depths, ordering
    /// by this value puts every platform after everything it depends on.
    /// </summary>
    public int GetDependencyDepth(PlatformInfo platform) => _depths[platform];

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
