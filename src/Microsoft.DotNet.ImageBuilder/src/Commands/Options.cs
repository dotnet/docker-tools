// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Reflection;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class Options : IOptions
    {
        public bool IsDryRun { get; set; }
        public bool IsVerbose { get; set; }

        public string? GetOption(string name)
        {
            string? result;

            PropertyInfo? propInfo = GetType().GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (propInfo != null)
            {
                result = propInfo.GetValue(this)?.ToString() ?? "";
            }
            else
            {
                result = null;
            }

            return result;
        }
    }

    public class CliOptionsBuilder
    {
        public virtual IEnumerable<Argument> GetCliArguments() => Enumerable.Empty<Argument>();
        public virtual IEnumerable<Option> GetCliOptions() =>
            new Option[]
            {
                CreateOption<bool>("dry-run", nameof(Options.IsDryRun),
                    "Dry run of what images get built and order they would get built in"),
                CreateOption<bool>("verbose", nameof(Options.IsVerbose),
                    "Show details about the tasks run")
            };
    }
}
#nullable disable
