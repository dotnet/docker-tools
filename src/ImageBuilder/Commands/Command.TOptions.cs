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
            Command cmd = new Command(this.GetCommandName(), Description);

            TOptions template = new();

            foreach (Argument argument in template.GetCliArguments())
            {
                cmd.Add(argument);
            }

            foreach (Option option in template.GetCliOptions())
            {
                cmd.Add(option);
            }

            foreach (Action<CommandResult> validator in template.GetValidators())
            {
                cmd.Validators.Add(validator);
            }

            cmd.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
            {
                TOptions options = new();
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
