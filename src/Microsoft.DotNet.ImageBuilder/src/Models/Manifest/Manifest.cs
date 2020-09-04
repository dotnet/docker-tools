// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest
{
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
            "Relative path to the GitHub readme Markdown file associated with the manifest. " +
            "This readme file documents the overall set of Docker repositories described by " +
            "the manifest.")]
        public string Readme { get; set; }

        [Description(
            "Relative path to the template the readme is generated from."
            )]
        public string ReadmeTemplate { get; set; }

        [Description(
            "The location of the Docker registry where the images are to be published."
            )]
        public string Registry { get; set; }

        [Description(
            "The set of Docker repositories described by this manifest."
            )]
        public Repo[] Repos { get; set; }

        [Description(
            "A set of custom variables that can be referenced in various parts of the " +
            "manifest. This provides a few benefits: 1) allows a commmonly used value " +
            "to be defined only once and referenced by its variable name many times" + 
            "2) allows tools that consume the manifest file to provide a mechanism to " +
            "dynamically override the value of these variables. Variables may be " +
            "referenced in other parts of the manifest by using the following syntax: " +
            "$(_VariableName_).")]
        public IDictionary<string, string> Variables { get; set; }

        public Manifest()
        {
        }
    }
}
