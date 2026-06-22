// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OrasProject.Oras.Oci;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Content and descriptor metadata for a pulled OCI blob.
/// </summary>
/// <param name="Content">The blob content.</param>
/// <param name="Descriptor">The descriptor for the pulled content blob.</param>
public sealed record OciBlob(byte[] Content, Descriptor Descriptor);
