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
    /// ImageBuilder metadata referrer for image build information stored in manifest annotations.
    /// </summary>
    public const string ImageInfo = "application/vnd.microsoft.imagebuilder.image-info.v1";

    /// <summary>
    /// Notary v2 signature envelope.
    /// </summary>
    public const string NotarySignatureV2 = "application/vnd.cncf.notary.signature";

    /// <summary>
    /// Microsoft artifact lifecycle metadata.
    /// </summary>
    public const string Lifecycle = "application/vnd.microsoft.artifact.lifecycle";
}
