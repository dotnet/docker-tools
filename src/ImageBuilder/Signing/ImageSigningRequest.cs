// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Notary;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Request to sign a container image.
/// </summary>
/// <param name="ImageName">Full tag/reference to manifest or manifest list.</param>
/// <param name="Descriptor">OCI descriptor for the image, used as the subject when pushing signatures.</param>
/// <param name="Payload">Notary v2 signing payload.</param>
public sealed record ImageSigningRequest(
    string ImageName,
    OrasDescriptor Descriptor,
    Payload Payload);
