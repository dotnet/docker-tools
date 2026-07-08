// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Well-known OCI artifact type identifiers.
/// </summary>
public static class OciArtifactType
{
    /// <summary>
    /// Notary v2 signature envelope.
    /// </summary>
    public const string NotarySignatureV2 = "application/vnd.cncf.notary.signature";

    /// <summary>
    /// Microsoft artifact lifecycle metadata.
    /// </summary>
    public const string Lifecycle = "application/vnd.microsoft.artifact.lifecycle";

    /// <summary>
    /// Referrer artifact that records build metadata (source repo, revision, base image, Dockerfile)
    /// for a single image as OCI annotations on the referrer manifest. Attached to each built image
    /// so that rebuild decisions can be made from the registry without an external image-info store.
    /// </summary>
    public const string ImageInfoReferrer = "application/vnd.microsoft.imagebuilder.image-info.v1+json";
}
