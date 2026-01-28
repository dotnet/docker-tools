// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Result of a manifest query from <see cref="IManifestService"/>.
/// </summary>
public class ManifestQueryResult
{
    /// <summary>
    /// Digest of the manifest content.
    /// </summary>
    public string ContentDigest { get; }

    /// <summary>
    /// JSON representation of the manifest content.
    /// </summary>
    public JsonObject Manifest { get; }

    public ManifestQueryResult(string contentDigest, JsonObject manifest)
    {
        ContentDigest = contentDigest;
        Manifest = manifest;
    }
}
