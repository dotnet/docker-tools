// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing container images and pushing signatures to the registry.
/// </summary>
public interface IImageSigningService
{
    /// <summary>
    /// Signs all images described in the artifact details by resolving OCI descriptors, signing payloads via ESRP,
    /// and pushing signature artifacts to the registry.
    /// </summary>
    /// <param name="imageArtifactDetails">The image artifact details containing platform and manifest list digests.</param>
    /// <param name="signingKeyCode">Certificate ID used by DDSignFiles.dll.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Results containing the digest of each signature artifact pushed to the registry.
    /// The order of the returned items is not guaranteed.
    /// </returns>
    Task<IReadOnlyList<ImageSigningResult>> SignImagesAsync(
        ImageArtifactDetails imageArtifactDetails,
        int signingKeyCode,
        CancellationToken cancellationToken = default);
}
