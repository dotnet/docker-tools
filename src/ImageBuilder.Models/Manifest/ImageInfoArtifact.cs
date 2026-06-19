// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

[Description("Describes where to publish an info about what images were built."
    + " The info will be published as an OCI artifact containing json.")]
public class ImageInfoArtifact()
{
    [Description("The repo where the artifact will be published, not including the registry.")]
    [JsonProperty(Required = Required.Always)]
    public string Repo { get; set; } = string.Empty;

    [Description("Tags for the OCI artifact. Requires at least one.")]
    [JsonProperty(Required = Required.Always)]
    public IDictionary<string, Tag> Tags { get; set; } = new Dictionary<string, Tag>();
}
