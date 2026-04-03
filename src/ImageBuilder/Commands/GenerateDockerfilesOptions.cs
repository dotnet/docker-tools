// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateDockerfilesOptions : GenerateArtifactsOptions, IDockerfileFilterableOptions
    {
        public DockerfileFilterOptions Dockerfile { get; set; } = new DockerfileFilterOptions();

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..Dockerfile.GetCliOptions(),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..Dockerfile.GetCliArguments(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            Dockerfile.Bind(result);
        }
    }
}
