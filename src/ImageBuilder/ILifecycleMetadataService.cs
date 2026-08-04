// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Oci;

namespace Microsoft.DotNet.ImageBuilder;

public interface ILifecycleMetadataService
{
    /// <summary>
    /// Gets the lifecycle artifact that refers to the given digest.
    /// </summary>
    /// <param name="digest">Fully-qualified digest reference (e.g., "registry.io/repo@sha256:...").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lifecycle artifact manifest, or null if none exists.</returns>
    Task<Manifest?> GetLifecycleArtifactAsync(string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Annotates the given digest with an end-of-life date.
    /// </summary>
    /// <param name="digest">Fully-qualified digest reference (e.g., "registry.io/repo@sha256:...").</param>
    /// <param name="date">The end-of-life date to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created lifecycle artifact manifest, or null on failure.</returns>
    Task<Manifest?> AnnotateEolDigestAsync(string digest, DateOnly date, CancellationToken cancellationToken = default);
}
