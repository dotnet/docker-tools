// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

public interface ILifecycleMetadataService
{
    /// <summary>
    /// Attaches EOL metadata and skips matching dates. Conflicting dates are skipped unless
    /// <paramref name="stopOnConflict"/> is set. Public requests ignore internal-only metadata.
    /// Throws on attachment failure or, when <paramref name="stopOnConflict"/> is set, on a conflict.
    /// </summary>
    Task<LifecycleMetadataAttachmentResult> AttachLifecycleMetadataAsync(
        string digest,
        DateOnly date,
        bool markAsInternal,
        bool stopOnConflict,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the most recently created lifecycle artifact that refers to the given digest.
    /// </summary>
    /// <param name="digest">Fully-qualified digest reference (e.g., "registry.io/repo@sha256:...").</param>
    /// <param name="includeInternal">
    /// Whether to consider internal-only lifecycle artifacts. When false, only public lifecycle
    /// artifacts are considered.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recently created lifecycle artifact, or null if none exists.</returns>
    Task<LifecycleArtifact?> GetLatestLifecycleArtifactAsync(
        string digest,
        bool includeInternal,
        CancellationToken cancellationToken);
}
