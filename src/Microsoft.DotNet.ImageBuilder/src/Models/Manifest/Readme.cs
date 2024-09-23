// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
public record Readme
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

    [Description(
            "Whether to use relative links to refer to Dockerfiles"
            )]
    public bool UseRelativeLinks { get; set; } = false;

    public Readme()
    {
    }

    public Readme(string path, string? templatePath, bool useRelativeLinks = false)
    {
        Path = path;
        TemplatePath = templatePath;
        UseRelativeLinks = useRelativeLinks;
    }
}
#nullable disable
