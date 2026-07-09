// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OrasProject.Oras.Oci;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Well-known OCI artifact type identifiers.
/// </summary>
public static class OciArtifactType
{
    /// <summary>
    /// The "empty" OCI artifact type. Used for referrer artifacts whose data is carried entirely in
    /// the manifest's annotations rather than in a content blob, matching the behavior of
    /// <c>oras attach</c> when no files are provided.
    /// </summary>
    public const string Empty = MediaType.EmptyJson;

    /// <summary>
    /// Notary v2 signature envelope.
    /// </summary>
    public const string NotarySignatureV2 = "application/vnd.cncf.notary.signature";

    /// <summary>
    /// Microsoft artifact lifecycle metadata.
    /// </summary>
    public const string Lifecycle = "application/vnd.microsoft.artifact.lifecycle";

}
