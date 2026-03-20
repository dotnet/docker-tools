// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

public class Readme
{
    [Description(
            "Relative path to the GitHub readme Markdown file associated with the " +
            "repository. This readme file documents the set of Docker images for " +
            "this repository."
            )]
    [JsonProperty(Required = Required.Always)]
    public string Path { get; set; } = string.Empty;

    [Description(
            "Relative path to the template the readme is generated from."
            )]
    public string? TemplatePath { get; set; }

    public Readme()
    {
    }

    public Readme(string path, string? templatePath)
    {
        Path = path;
        TemplatePath = templatePath;
    }
}
