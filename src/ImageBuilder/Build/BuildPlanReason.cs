// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Identifies a condition that affected a platform's build action.
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
