// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Well-known OCI image annotation keys applied to built images as Docker labels.
/// See https://github.com/opencontainers/image-spec/blob/main/annotations.md.
/// </summary>
public static class OciAnnotations
{
    /// <summary>
    /// URL of the source code repository the image was built from.
    /// </summary>
    public const string Source = "org.opencontainers.image.source";

    /// <summary>
    /// Source control revision (commit) the image was built from.
    /// </summary>
    public const string Revision = "org.opencontainers.image.revision";

    /// <summary>
    /// Image reference of the base image the image was built from.
    /// </summary>
    public const string BaseName = "org.opencontainers.image.base.name";

    /// <summary>
    /// Digest of the base image the image was built from.
    /// </summary>
    public const string BaseDigest = "org.opencontainers.image.base.digest";
}
