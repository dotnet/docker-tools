// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Reflection;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class Options : IOptions
    {
        public bool IsDryRun { get; set; }
        public bool IsVerbose { get; set; }
        public bool NoVersionLogging { get; set; }
    }

    public class CliOptionsBuilder
    {
        /// <summary>
        /// Arguments are positional, non-optional parameters that must be passed to the command.
        /// </summary>
        /// <returns>Collection of Arguments</returns>
        public virtual IEnumerable<Argument> GetCliArguments() => [];

        /// <summary>
        /// Options are optional, non-positional parameters or flags that can be passed to the command by name.
        /// </summary>
        /// <returns>Collection of Options</returns>
        public virtual IEnumerable<Option> GetCliOptions() =>
            [
                CreateOption<bool>(
                    alias: "dry-run",
                    propertyName: nameof(Options.IsDryRun),
                    description: "Dry run of what images get built and order they would get built in"),
                CreateOption<bool>(
                    alias: "verbose",
                    propertyName: nameof(Options.IsVerbose),
                    description: "Show details about the tasks run"),
                CreateOption<bool>(
                    alias: "no-version-logging",
                    propertyName: nameof(Options.NoVersionLogging),
                    description: "Disable automatic logging of Docker version information")
            ];
    }
}
