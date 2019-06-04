// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ManifestFilterOptions
    {
        public string Architecture { get; set; }
        public string OsType { get; set; }
        public string OsVersion { get; set; }
        public IEnumerable<string> Paths { get; set; }

        public void ParseCommandLine(ArgumentSyntax syntax)
        {
            string architecture = DockerHelper.Architecture.GetDockerName();
            syntax.DefineOption(
                "architecture",
                ref architecture,
                "Architecture of Dockerfiles to operate on - wildcard chars * and ? supported (default is current OS architecture)");
            Architecture = architecture;

            string osType = DockerHelper.OS.GetDockerName();
            syntax.DefineOption(
                "os-type",
                ref osType,
                "OS type (linux/windows) of the Dockerfiles to build - wildcard chars * and ? supported (default is the Docker OS)");
            OsType = osType;

            string osVersion = null;
            syntax.DefineOption(
                "os-version",
                ref osVersion,
                "OS version of the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)");
            OsVersion = osVersion;

            IReadOnlyList<string> paths = Array.Empty<string>();
            syntax.DefineOptionList(
                "path",
                ref paths,
                "Directory paths containing the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)");
            Paths = paths;
        }
    }
}
