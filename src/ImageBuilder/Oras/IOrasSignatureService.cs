// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Signing;
using OrasProject.Oras.Oci;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Service for pushing Notary v2 signatures to a registry as referrer artifacts.
/// </summary>
public interface IOrasSignatureService
{
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
}
