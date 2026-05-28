// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel;

public static class PlatformInfoExtensions
{
    /// <summary>
    /// Returns a single tag that can be used as a stable identifier for this platform
    /// when looking it up in a registry (e.g. for digest lookups).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the platform has no tags defined in the manifest. A shared-tag
    /// platform without any concrete tags is a manifest configuration error.
    /// </exception>
    public static TagInfo GetRepresentativeTag(this PlatformInfo platform) =>
        platform.Tags.First();
}
