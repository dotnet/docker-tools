// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

[Description(
    "The manifest file is the primary source of metadata that drives the production " +
    "of all .NET Docker images.  It describes various attributes of the Docker images " +
    "that are to be produced by a given GitHub repo. .NET Docker's engineering system " +
    "consumes this file in various ways as part of the automated build pipelines and " +
    "other tools. It's intended to be product-agnostic meaning that it could be used " +
    "to describe metadata for Docker image production of any product, not just .NET.")]
public class Manifest
{
    [Description(
        "Additional json files to be loaded with this manifest.  This is a convienent" +
        "way to split the manifest apart into logical parts."
        )]
    public string[] Includes { get; set; }

    [Description(
        "Info about the readme that documents the product family."
        )]
    public Readme Readme { get; set; }

    [Description(
        "The location of the Docker registry where the images are to be published."
        )]
    public string Registry { get; set; }

    [Description(
        "The set of Docker repositories described by this manifest."
        )]
    public Repo[] Repos { get; set; } = Array.Empty<Repo>();

    [Description(
        "A set of custom variables that can be referenced in various parts of the " +
        "manifest. This provides a few benefits: 1) allows a commmonly used value " +
        "to be defined only once and referenced by its variable name many times" +
        "2) allows tools that consume the manifest file to provide a mechanism to " +
        "dynamically override the value of these variables. Variables may be " +
        "referenced in other parts of the manifest by using the following syntax: " +
        "$(_VariableName_).")]
    public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

    public Manifest()
    {
    }
}
