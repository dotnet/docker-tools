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
/// Immutable decisions for a set of manifest platforms.
/// </summary>
public sealed class BuildPlan
{
    private readonly IReadOnlyDictionary<PlatformInfo, BuildPlanEntry> _entriesByPlatform;
    private readonly ManifestInfo _manifest;

    /// <summary>
    /// Creates a plan from decisions for unique manifest platforms.
    /// </summary>
    public BuildPlan(ManifestInfo manifest, IEnumerable<BuildPlanEntry> entries)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        ArgumentNullException.ThrowIfNull(entries);
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
        PlatformInfo[] plannedPlatforms = Entries.Select(entry => entry.Platform).ToArray();
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
            IEnumerable<PlatformInfo> requiredPlatforms = _manifest
                .GetDescendants(
                    origin,
                    plannedPlatforms,
                    includeAncestorsOfDescendants: true)
                .Prepend(origin);

            platforms.AddRange(requiredPlatforms.Where(addedPlatforms.Add));
        }

        return platforms;
    }
}
