// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Annotation keys, in ImageBuilder's own namespace, that describe how the subject image was built.
/// These are recorded on the image's referrer artifact.
/// </summary>
/// <remarks>
/// Standard <c>org.opencontainers.image.*</c> annotations are deliberately not used here because OCI
/// annotations describe the artifact they are placed on. On a referrer artifact they would describe the
/// referrer itself rather than the subject image, so a custom namespace is used to describe the subject.
/// </remarks>
public static class ImageBuilderAnnotations
{
    /// <summary>
    /// URL of the source code repository the image was built from.
    /// </summary>
    public const string Source = "vnd.microsoft.imagebuilder.source";

    /// <summary>
    /// Source control revision (commit) the image was built from.
    /// </summary>
    public const string Revision = "vnd.microsoft.imagebuilder.revision";

    /// <summary>
    /// Path of the Dockerfile the image was built from, relative to the root of the source repository.
    /// </summary>
    public const string Dockerfile = "vnd.microsoft.imagebuilder.dockerfile";

    /// <summary>
    /// Image reference of the base image the image was built from.
    /// </summary>
    public const string BaseName = "vnd.microsoft.imagebuilder.base.name";

    /// <summary>
    /// Digest of the base image the image was built from.
    /// </summary>
    public const string BaseDigest = "vnd.microsoft.imagebuilder.base.digest";
}
