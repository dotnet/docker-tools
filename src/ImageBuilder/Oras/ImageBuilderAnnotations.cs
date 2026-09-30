// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// OCI annotation keys owned by ImageBuilder.
/// </summary>
public static class ImageBuilderAnnotations
{
    /// <summary>
    /// Marks an artifact as internal-only. Any command or process may set this annotation to
    /// <c>true</c> on any artifact. ImageBuilder never copies referrers marked this way when
    /// publishing images.
    /// </summary>
    public const string Internal = "vnd.microsoft.dotnet.imagebuilder.internal";

    extension(ReferrerInfo referrer)
    {
        /// <summary>
        /// Whether the referrer is marked internal-only via <see cref="Internal"/>.
        /// </summary>
        public bool IsInternal =>
            referrer.Annotations?.TryGetValue(Internal, out string? value) == true
            && bool.TryParse(value, out bool isInternal)
            && isInternal;
    }
}
