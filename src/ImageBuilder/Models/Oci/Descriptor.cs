#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Oci;

/// <summary>
/// Subset of OCI Descriptor spec containing only required properties:
/// https://github.com/opencontainers/image-spec/blob/39ab2d54cfa8fe1bee1ff20001264986d92ab85a/descriptor.md
/// </summary>
public sealed record Descriptor(string MediaType, string Digest, long Size)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static Descriptor FromJson(string json) => JsonSerializer.Deserialize<Descriptor>(json, JsonOptions);
}
