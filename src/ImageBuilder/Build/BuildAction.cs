// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Describes how a platform should be handled by build execution.
/// </summary>
public enum BuildAction
{
    /// <summary>The platform must be built.</summary>
    Build,

    /// <summary>The previously published platform can be reused without changes.</summary>
    Reuse,

    /// <summary>The previously published image can be reused but this platform's tags must be published.</summary>
    ReuseAndPublishTags
}
