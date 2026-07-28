// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// What a check's answer depends on.
/// </summary>
public enum BuildPlanCheckScope
{
    /// <summary>
    /// Depends only on the Dockerfile and its base image, so the answer is shared by every platform
    /// built from the same Dockerfile and build args.
    /// </summary>
    ImageContent,

    /// <summary>
    /// Depends on an individual platform's published tags and recorded digest.
    /// </summary>
    PlatformPublication
}
