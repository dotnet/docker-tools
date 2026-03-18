// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class Options : IOptions
    {
        public bool IsDryRun { get; set; }
        public bool IsVerbose { get; set; }
        public bool NoVersionLogging { get; set; }

        private static readonly Option<bool> DryRunOption = new(CliHelper.FormatAlias("dry-run"))
        {
            Description = "Dry run of what images get built and order they would get built in"
        };

        private static readonly Option<bool> VerboseOption = new(CliHelper.FormatAlias("verbose"))
        {
            Description = "Show details about the tasks run"
        };

        private static readonly Option<bool> NoVersionLoggingOption = new(CliHelper.FormatAlias("no-version-logging"))
        {
            Description = "Disable automatic logging of Docker version information"
        };

        /// <summary>
        /// Arguments are positional, non-optional parameters that must be passed to the command.
        /// </summary>
        public virtual IEnumerable<Argument> GetCliArguments() => [];

        /// <summary>
        /// Options are optional, non-positional parameters or flags that can be passed to the command by name.
        /// </summary>
        public virtual IEnumerable<Option> GetCliOptions() =>
            [DryRunOption, VerboseOption, NoVersionLoggingOption];

        /// <summary>
        /// Optional delegates for performing additional validation of arguments and options.
        /// </summary>
        /// <remarks>
        /// Validators should call <see cref="CommandResult.AddError"/> to report errors.
        /// Validation failures will manifest the same way as any other command line parsing errors.
        /// </remarks>
        public virtual IEnumerable<System.Action<CommandResult>> GetValidators() => [];

        /// <summary>
        /// Binds parsed command line values to this Options instance.
        /// </summary>
        public virtual void Bind(ParseResult result)
        {
            IsDryRun = result.GetValue(DryRunOption);
            IsVerbose = result.GetValue(VerboseOption);
            NoVersionLogging = result.GetValue(NoVersionLoggingOption);
        }
    }
}
