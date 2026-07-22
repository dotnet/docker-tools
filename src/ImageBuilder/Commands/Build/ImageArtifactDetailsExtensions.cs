// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Commands.Build;

/// <summary>
/// Extension methods for image artifact details used during build command processing.
/// </summary>
internal static class ImageArtifactDetailsExtensions
{
    /// <summary>
    /// Enumerates all platform data entries from image-info repo and image groups.
    /// </summary>
    /// <param name="imageArtifactDetails">The image artifact details to enumerate.</param>
    /// <returns>All platform data entries contained in the image artifact details.</returns>
    internal static IEnumerable<PlatformData> EnumeratePlatforms(this ImageArtifactDetails imageArtifactDetails) =>
        imageArtifactDetails.Repos
            .Where(repoData => repoData.Images != null)
            .SelectMany(repoData => repoData.Images)
            .SelectMany(imageData => imageData.Platforms);
}
