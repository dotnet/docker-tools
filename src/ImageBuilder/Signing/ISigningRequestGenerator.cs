// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Generates signing requests from image artifact details.
/// </summary>
public interface ISigningRequestGenerator
{
    /// <summary>
    /// Creates signing requests for platform images from ImageArtifactDetails.
    /// </summary>
    /// <param name="imageArtifactDetails">The image artifact details containing platform digests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Signing requests for each platform image with a digest.</returns>
    Task<IReadOnlyList<ImageSigningRequest>> GeneratePlatformSigningRequestsAsync(
        ImageArtifactDetails imageArtifactDetails,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates signing requests for manifest lists from ImageArtifactDetails.
    /// </summary>
    /// <param name="imageArtifactDetails">The image artifact details containing manifest list digests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Signing requests for each manifest list with a digest.</returns>
    Task<IReadOnlyList<ImageSigningRequest>> GenerateManifestListSigningRequestsAsync(
        ImageArtifactDetails imageArtifactDetails,
        CancellationToken cancellationToken = default);
}
