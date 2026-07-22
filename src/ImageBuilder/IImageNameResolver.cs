// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Resolves image names and tags for FROM instructions across different contexts
/// (e.g. local builds, registry pulls, digest queries).
/// </summary>
public interface IImageNameResolver
{
    /// <summary>
    /// Returns the tag to use for interacting with the image of a FROM instruction that has been pulled or built locally.
    /// </summary>
    /// <param name="fromImage">Tag of the FROM image.</param>
    string GetFromImageLocalTag(string fromImage);

    /// <summary>
    /// Returns the tag to use for pulling the image of a FROM instruction.
    /// </summary>
    /// <param name="fromImage">Tag of the FROM image.</param>
    string GetFromImagePullTag(string fromImage);

    /// <summary>
    /// Returns the tag that represents the publicly available tag of a FROM instruction.
    /// </summary>
    /// <param name="fromImage">Tag of the FROM image.</param>
    string GetFromImagePublicTag(string fromImage);

    /// <summary>
    /// Returns the image name to use when querying for the digest of the final stage base image.
    /// </summary>
    /// <param name="platform">Platform whose final stage image name is resolved.</param>
    string GetFinalStageImageNameForDigestQuery(PlatformInfo platform);
}
