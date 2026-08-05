// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Work that ImageBuilder must perform for a target.
/// </summary>
public enum BuildAction
{
    /// <summary>
    /// The published image is valid and this invocation does not need it locally.
    /// </summary>
    NoAction = 0,

    /// <summary>
    /// Use the valid published image without running a Docker build. The image may need to be
    /// pulled, imported, or retagged for this invocation.
    /// </summary>
    UsePublishedImage = 1,

    /// <summary>
    /// Use the valid published image and continue it through downstream processing because its
    /// published metadata, such as tags, must be updated.
    /// </summary>
    PublishExistingImage = 2,

    /// <summary>
    /// Run a Docker build for the target.
    /// </summary>
    BuildImage = 3
}

/// <summary>
/// An explanation for a planned action, optionally linked to the reason that caused it.
/// </summary>
public sealed record BuildReason(
    string Message,
    BuildReason? Cause = null);

/// <summary>
/// Published image-info associated with a build target.
/// </summary>
/// <param name="Source">Target whose image-info supplied the data.</param>
/// <param name="Image">Existing published platform data.</param>
/// <param name="SharedTags">Published image-level tags stored outside <paramref name="Image"/>.</param>
public sealed record PublishedImage(
    BuildTarget Source,
    PlatformData Image,
    IReadOnlyList<string> SharedTags);

public sealed record BuildPlanItem(
    BuildTarget Target,
    BuildAction Action,
    IReadOnlyList<BuildReason> Reasons,
    PublishedImage? PublishedImage);
