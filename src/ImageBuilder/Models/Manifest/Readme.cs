// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Manifest;

#nullable enable
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
#nullable disable
