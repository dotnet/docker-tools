// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Signing;
using OrasProject.Oras.Oci;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Service for resolving OCI descriptors and pushing Notary v2 signatures to a registry.
/// </summary>
public interface IOrasService
{
    /// <summary>
    /// Resolves a registry reference to its OCI descriptor.
    /// </summary>
    /// <param name="reference">Full registry reference (e.g., "registry.io/repo@sha256:...").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCI descriptor containing mediaType, digest, and size.</returns>
    Task<Descriptor> GetDescriptorAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the manifest for a registry reference, returning both its digest and parsed JSON body.
    /// </summary>
    /// <param name="reference">Full registry reference (e.g., "registry.io/repo:tag" or "registry.io/repo@sha256:...").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manifest digest and parsed JSON content.</returns>
    Task<ManifestQueryResult> GetManifestAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a signed payload to the registry as a referrer artifact.
    /// </summary>
    /// <param name="subjectDescriptor">The descriptor of the image being signed.</param>
    /// <param name="result">The signed payload and certificate chain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The digest of the signature artifact that was pushed.</returns>
    Task<string> PushSignatureAsync(
        Descriptor subjectDescriptor,
        PayloadSigningResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the OCI referrers for the given image.
    /// </summary>
    /// <param name="reference">Full registry reference (e.g., "registry.io/repo:tag" or "registry.io/repo@sha256:...").</param>
    /// <param name="isDryRun">When true, skips registry calls and returns an empty list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of <see cref="ReferrerInfo"/> containing the digest reference and artifact type
    /// for every referrer associated with the image.
    /// </returns>
    Task<IReadOnlyList<ReferrerInfo>> GetReferrersAsync(
        string reference,
        bool isDryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and pushes a referrer artifact with the given type and annotations.
    /// </summary>
    /// <param name="reference">Full registry reference of the subject image (e.g., "registry.io/repo@sha256:...").</param>
    /// <param name="artifactType">The OCI artifact type for the referrer.</param>
    /// <param name="annotations">Annotations to set on the referrer manifest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The digest of the created referrer artifact.</returns>
    Task<string> AttachArtifactAsync(
        string reference,
        string artifactType,
        IDictionary<string, string> annotations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes content to the registry as a single standalone OCI artifact and applies one or
    /// more tags to it, so that the same artifact can later be pulled by any of those tags.
    /// </summary>
    /// <param name="content">The artifact content to store as the single layer (e.g., image-info JSON).</param>
    /// <param name="mediaType">The media type of the content layer.</param>
    /// <param name="artifactType">The OCI artifact type recorded on the manifest.</param>
    /// <param name="registry">The target registry host (e.g., "myregistry.azurecr.io").</param>
    /// <param name="repository">The target repository within the registry (e.g., "dotnet/versions").</param>
    /// <param name="tags">The tags to apply to the pushed artifact. At least one tag is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The digest of the single pushed artifact manifest.</returns>
    Task<string> PushArtifactAsync(
        byte[] content,
        string mediaType,
        string artifactType,
        string registry,
        string repository,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls content layers from a standalone OCI artifact tag.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "myregistry.azurecr.io").</param>
    /// <param name="repository">The repository within the registry (e.g., "dotnet/versions").</param>
    /// <param name="tag">The artifact tag to pull.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCI artifact manifest/descriptors and pulled blob content.</returns>
    Task<OciArtifact> PullAsync(
        string registry,
        string repository,
        string tag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the blob identified by the given descriptor.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "myregistry.azurecr.io").</param>
    /// <param name="repository">The repository within the registry (e.g., "dotnet/versions").</param>
    /// <param name="descriptor">The descriptor of the blob to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream for the blob content. The caller is responsible for disposing it.</returns>
    Task<Stream> FetchBlobAsync(
        string registry,
        string repository,
        Descriptor descriptor,
        CancellationToken cancellationToken = default);
}
