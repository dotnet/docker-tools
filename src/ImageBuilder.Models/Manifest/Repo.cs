// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

[Description(
    "A repository object contains metadata about a target Docker repository " +
    "and the images to be contained in it."
    )]
public class Repo
{
    [Description(
        "A unique identifier of the repo. This is purely within the context " +
        "of the manifest and not exposed to Docker in any way."
        )]
    public string Id { get; set; }

    [Description(
        "The set of images contained in this repository."
        )]
    [JsonProperty(Required = Required.Always)]
    public Image[] Images { get; set; }

    [Description(
        "Relative path to the MCR tags template YAML file that is used by " +
        "tooling to generate the tags section of the readme file."
        )]
    public string McrTagsMetadataTemplate { get; set; }

    [Description(
        "The name of the Docker repository where the described images are to " +
        "be published (example: dotnet/core/runtime)."
        )]
    [JsonProperty(Required = Required.Always)]
    public string Name { get; set; }

    [Description(
        "Info about the readme that documents the repo."
        )]
    public Readme[] Readmes { get; set; } = Array.Empty<Readme>();

    public Repo()
    {
    }
}
