// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest
{
    [Description(
        "A platform object contains metadata about a platform-specific version of an " +
        "image and refers to the actual Dockerfile used to build the image.")]
    public class Platform
    {
        [Description(
            "The processor architecture associated with the image."
            )]
        [DefaultValue(Architecture.AMD64)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Architecture Architecture { get; set; }

        [Description(
            "A set of values that will passed to the `docker build` command " +
            "to override variables defined in the Dockerfile.")]
        public IDictionary<string, string> BuildArgs { get; set; }

        [Description(
            "Relative path to the associated Dockerfile. This can be a file or a " +
            "directory. If it is a directory, the file name defaults to Dockerfile."
            )]
        [JsonProperty(Required = Required.Always)]
        public string Dockerfile { get; set; }

        [Description(
            "Relative path to the template the Dockerfile is generated from."
            )]
        public string DockerfileTemplate { get; set; }

        [Description(
            "The generic name of the operating system associated with the image."
            )]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(Required = Required.Always)]
        public OS OS { get; set; }

        [Description(
            "The specific version of the operating system associated with the image. " +
            "Examples: alpine3.9, bionic, nanoserver-1903."
            )]
        [JsonProperty(Required = Required.Always)]
        public string OsVersion { get; set; }

        [Description(
            "The set of platform-specific tags associated with the image."
            )]
        [JsonProperty(Required = Required.Always)]
        public IDictionary<string, Tag> Tags { get; set; }

        [Description(
            "The custom build leg groupings associated with the platform."
            )]
        public CustomBuildLegGrouping[] CustomBuildLegGrouping { get; set; } = Array.Empty<CustomBuildLegGrouping>();

        [Description(
            "A label which further distinguishes the architecture when it " +
            "contains variants. For example, the ARM architecture has variants " +
            "named v6, v7, etc."
            )]
        public string Variant { get; set; }

        public Platform()
        {
        }
    }
}
