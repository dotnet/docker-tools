// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.DotNet.DockerTools.ImageBuilder.Models.Oci;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Notary;

/// <summary>
/// Notary Signature Envelope Payload spec:
/// https://github.com/notaryproject/specifications/blob/00abcea0ad35bb80c74cb82890834440bf8f218d/specs/signature-specification.md#payload
/// </summary>
public sealed record Payload(Descriptor TargetArtifact)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, s_jsonOptions);
    }

    public static Payload FromJson(string json)
    {
        return JsonSerializer.Deserialize<Payload>(json, s_jsonOptions);
    }
}
