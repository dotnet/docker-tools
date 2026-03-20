// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Represents an OCI referrer associated with a registry artifact.
/// </summary>
/// <param name="Digest">Fully-qualified digest reference (e.g., "registry.io/repo@sha256:abc...").</param>
/// <param name="ArtifactType">The OCI artifact type (e.g., "application/vnd.cncf.notary.signature"), or null if not set.</param>
public record ReferrerInfo(string Digest, string? ArtifactType);
