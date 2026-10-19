#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.ImageBuilder.Models.Oci;

namespace Microsoft.DotNet.ImageBuilder.Models.Notary;

/// <summary>
/// Notary Signature Envelope Payload spec:
/// https://github.com/notaryproject/specifications/blob/00abcea0ad35bb80c74cb82890834440bf8f218d/specs/signature-specification.md#payload
/// </summary>
public sealed record Payload(Descriptor TargetArtifact)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static Payload FromJson(string json) => JsonSerializer.Deserialize<Payload>(json, JsonOptions);
}
