// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    /// Creates signing requests for all images (platforms and manifest lists) in the given artifact details.
    /// </summary>
    /// <param name="imageArtifactDetails">The image artifact details containing platform and manifest list digests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Signing requests for each digest in <paramref name="imageArtifactDetails"/>.
    /// There are no guarantees about the ordering of the returned requests.
    /// </returns>
    Task<IReadOnlyList<ImageSigningRequest>> GenerateSigningRequestsAsync(
        ImageArtifactDetails imageArtifactDetails,
        CancellationToken cancellationToken = default);
}
