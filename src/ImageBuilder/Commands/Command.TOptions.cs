// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class Command<TOptions> : ICommand
        where TOptions : Options, new()
    {
        public TOptions Options { get; private set; }

        Options ICommand.Options => Options;

        protected abstract string Description { get; }

        public Command()
        {
            Options = new TOptions();
        }

        public Command GetCliCommand()
        {
            TOptions options = new();

            Command cmd = BuildCliCommand(
                name: this.GetCommandName(),
                description: Description,
                options: options);

            cmd.SetAction(async (parseResult, cancellationToken) =>
            {
                options.Bind(parseResult);

                if (!options.NoVersionLogging)
                {
                    LogDockerVersions();
                }

                Initialize(options);
                await ExecuteAsync();
            });

            return cmd;
        }

        /// <summary>
        /// Builds a CLI command with the arguments, options, and validators defined by the given options instance.
        /// </summary>
        public static Command BuildCliCommand(string name, string description, TOptions options)
        {
            Command command = new(name, description);

            foreach (Argument argument in options.GetCliArguments())
                command.Add(argument);

            foreach (Option option in options.GetCliOptions())
                command.Add(option);

            foreach (Action<CommandResult> validator in options.GetValidators())
                command.Validators.Add(validator);

            return command;
        }

        protected virtual void Initialize(TOptions options)
        {
            Options = options;
        }

        private static void LogDockerVersions()
        {
            // Capture the Docker version and info in the output.
            ExecuteHelper.Execute(fileName: "docker", args: "version", isDryRun: false);
            ExecuteHelper.Execute(fileName: "docker", args: "info", isDryRun: false);
        }

        public abstract Task ExecuteAsync();
    }
}
