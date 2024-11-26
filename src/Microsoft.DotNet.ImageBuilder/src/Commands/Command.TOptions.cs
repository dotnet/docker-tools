// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class Command<TOptions, TOptionsBuilder> : ICommand
        where TOptions : Options, new()
        where TOptionsBuilder : CliOptionsBuilder, new()
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

            TOptionsBuilder OptionsBuilder = new TOptionsBuilder();

            foreach (Argument argument in OptionsBuilder.GetCliArguments())
            {
                cmd.AddArgument(argument);
            }

            foreach (Option option in OptionsBuilder.GetCliOptions())
            {
                cmd.AddOption(option);
            }

            cmd.Handler = CommandHandler.Create<TOptions>(options =>
            {
                if (Options.CIMode)
                {
                    LogDockerVersions();
                }

                Initialize(options);
                return ExecuteAsync();
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
#nullable disable
