// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ManifestFilterOptions
    {
        public PlatformFilterOptions Platform { get; set; } = new();

        public DockerfileFilterOptions Dockerfile { get; set; } = new();

        public IEnumerable<Option> GetCliOptions() =>
        [
            ..Platform.GetCliOptions(),
            ..Dockerfile.GetCliOptions(),
        ];

        public IEnumerable<Argument> GetCliArguments() =>
        [
            ..Platform.GetCliArguments(),
            ..Dockerfile.GetCliArguments(),
        ];

        public void Bind(ParseResult result)
        {
            Platform.Bind(result);
            Dockerfile.Bind(result);
        }
    }
}
