// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ManifestFilterOptions
    {
        public PlatformFilterOptions Platform { get; set; } = new();

        public DockerfileFilterOptions Dockerfile { get; set; } = new();

        public string Architecture => Platform.Architecture;

        public string OsType => Platform.OsType;

        public IEnumerable<string> OsVersions => Platform.OsVersions;

        public IEnumerable<string> Paths => Dockerfile.Paths;

        public IEnumerable<string> ProductVersions => Dockerfile.ProductVersions;
    }

    public class ManifestFilterOptionsBuilder
    {
        private readonly PlatformFilterOptionsBuilder _platformFilterOptionsBuilder = new();

        private readonly DockerfileFilterOptionsBuilder _dockerfileFilterOptionsBuilder = new();

        public IEnumerable<Option> GetCliOptions() =>
        [
            .._platformFilterOptionsBuilder.GetCliOptions(),
            .._dockerfileFilterOptionsBuilder.GetCliOptions(),
        ];

        public IEnumerable<Argument> GetCliArguments() =>
        [
            .._platformFilterOptionsBuilder.GetCliArguments(),
            .._dockerfileFilterOptionsBuilder.GetCliArguments(),
        ];
    }
}
