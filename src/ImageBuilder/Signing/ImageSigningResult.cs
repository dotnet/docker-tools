// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Result of signing a container image and pushing the signature to the registry.
/// </summary>
/// <param name="ImageName">Full tag/reference to manifest or manifest list.</param>
/// <param name="SignatureDigest">Digest of the signature artifact stored in the registry.</param>
public sealed record ImageSigningResult(
    string ImageName,
    string SignatureDigest);
