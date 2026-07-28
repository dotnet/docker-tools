// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Creates build plans for manifest platforms.
/// </summary>
public interface IBuildPlanner
{
    /// <summary>
    /// Decides how each platform should be handled and explains why.
    /// </summary>
    /// <param name="manifest">Manifest that the platforms belong to.</param>
    /// <param name="allPlatforms">
    /// Every platform that forms the dependency graph. This is usually wider than
    /// <paramref name="platformsToEvaluate"/> so that a build can be traced through platforms that
    /// were not themselves evaluated, and it must contain all of them.
    /// </param>
    /// <param name="platformsToEvaluate">
    /// The platforms to decide about. Platforms outside this set still take part in the dependency
    /// graph, but no decision is made about them.
    /// </param>
    /// <param name="imageArtifactDetails">Previously published image metadata, when available.</param>
    /// <param name="baseImageResolver">Resolver for current base-image digests.</param>
    /// <param name="sourceRepoUrl">
    /// Source repository URL used to compare Dockerfile commits. When null, the Dockerfile
    /// comparison is not performed.
    /// </param>
    /// <param name="useCache">Whether previously published images may be reused.</param>
    Task<BuildPlan> CreateBuildPlanAsync(
        ManifestInfo manifest,
        IEnumerable<PlatformInfo> allPlatforms,
        IEnumerable<PlatformInfo> platformsToEvaluate,
        ImageArtifactDetails? imageArtifactDetails,
        BaseImageResolver baseImageResolver,
        string? sourceRepoUrl,
        bool useCache);
}
