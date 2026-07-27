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
    /// Only platforms based on an external image are evaluated, because the freshness of a platform
    /// based on an internal image follows from the platform that produces that image. Dockerfile
    /// commits are not compared: the manifest may come from another repository, so no source repo
    /// URL is available and that check has no opinion.
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
            manifest.GetFilteredPlatformsWithExternalBaseImage(),
            manifest.GetAllPlatforms(),
            imageArtifactDetails,
            baseImageResolver,
            sourceRepoUrl: null,
            BuildPlanCheck.Default);

        return plan
            .GetPlatformsToSchedule(
                [BuildPlanReason.MissingImageInfo, BuildPlanReason.BaseImageChanged])
            .Select(platform => platform.Model.Dockerfile)
            .Distinct();
    }
}
