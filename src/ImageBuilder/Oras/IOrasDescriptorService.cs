// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Service for resolving OCI descriptors from registry references.
/// </summary>
public interface IOrasDescriptorService
{
    /// <summary>
    /// Resolves a registry reference to its OCI descriptor.
    /// </summary>
    /// <param name="reference">Full registry reference (e.g., "registry.io/repo@sha256:...").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCI descriptor containing mediaType, digest, and size.</returns>
    Task<Descriptor> GetDescriptorAsync(string reference, CancellationToken cancellationToken = default);
}
