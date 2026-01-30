// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Result of signing a payload via ESRP.
/// </summary>
/// <param name="ImageName">Full tag/reference to manifest or manifest list.</param>
/// <param name="SignedPayload">Signed payload file stored on disk.</param>
/// <param name="CertificateChain">Certificate chain in io.cncf.notary.x509chain.thumbprint#S256 format.</param>
public sealed record PayloadSigningResult(
    string ImageName,
    FileInfo SignedPayload,
    string CertificateChain);
