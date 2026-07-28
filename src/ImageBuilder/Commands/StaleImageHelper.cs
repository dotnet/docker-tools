// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Build;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public static class StaleImageHelper
{
    /// <summary>
    /// Gets the paths of the Dockerfiles that need to be rebuilt because an external base image
    /// changed or because an image has never been published, along with the paths that depend on
    /// them.
    /// </summary>
    /// <remarks>
    /// The whole manifest forms the dependency graph so that a stale base image can be traced to
    /// every Dockerfile affected by it, but only platforms that survive the command's filters and
    /// are based on an external image are evaluated.
    /// </remarks>
    public static async Task<IEnumerable<string>> GetStaleDockerfilePathsAsync(
        IBuildPlanner buildPlanner,
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        ImageDigestCache imageDigestCache,
        BaseImageOverrideOptions baseImageOverrideOptions,
        string? sourceRepoPrefix,
        bool isDryRun)
    {
        ImageNameResolverForMatrix imageNameResolver = new(
            baseImageOverrideOptions,
            manifest,
            repoPrefix: null,
            sourceRepoPrefix);
        BaseImageResolver baseImageResolver = BaseImageResolver.CreateForRegistryImages(
            imageDigestCache,
            imageNameResolver,
            isDryRun);
        BuildPlan plan = await buildPlanner.CreateBuildPlanAsync(
            manifest,
            dependencyPlatforms: manifest.GetAllPlatforms(),
            platformsToPlan: manifest.GetFilteredPlatformsWithExternalBaseImage(),
            imageArtifactDetails,
            baseImageResolver,
            // The manifest belongs to another repository, so its Dockerfiles are not on disk and
            // there is no commit URL to compare against. The Dockerfile check abstains, which is
            // what we want: a Dockerfile change is caught by that repository's own CI when the
            // change is made, and is not something this caller should trigger a build for.
            sourceRepoUrl: null,
            useCache: true);

        // Only a base image that moved, or an image that has never been published, is a reason for
        // this caller to ask for a build. Missing tags are deliberately not: the published image is
        // still current and only needs its tags applied, which a build already underway does on its
        // own. Triggering a build for that would rebuild an image that nothing has invalidated.
        return plan
            .GetPlatformsToSchedule(
                [BuildPlanReason.MissingImageInfo, BuildPlanReason.BaseImageChanged])
            .Select(platform => platform.Model.Dockerfile)
            .Distinct();
    }
}
