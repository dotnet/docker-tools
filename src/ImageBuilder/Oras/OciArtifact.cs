// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using OrasProject.Oras.Oci;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Content and manifest metadata pulled from an OCI artifact.
/// </summary>
/// <param name="ManifestDescriptor">The descriptor for the pulled OCI manifest.</param>
/// <param name="Manifest">The pulled OCI manifest.</param>
/// <param name="Blobs">The pulled content blobs.</param>
public sealed record OciArtifact(Descriptor ManifestDescriptor, Manifest Manifest, IReadOnlyList<OciBlob> Blobs);
