// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class Command<TOptions, TSymbolsBuilder> : ICommand
        where TOptions : Options, new()
        where TSymbolsBuilder : CliSymbolsBuilder, new()
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

            TSymbolsBuilder symbolsBuilder = new TSymbolsBuilder();

            foreach (Argument argument in symbolsBuilder.GetCliArguments())
            {
                cmd.AddArgument(argument);
            }

            foreach (Option option in symbolsBuilder.GetCliOptions())
            {
                cmd.AddOption(option);
            }

            cmd.Handler = CommandHandler.Create<TOptions>(options =>
            {
                Initialize(options);
                return ExecuteAsync();
            });

            return cmd;
        }

        protected virtual void Initialize(TOptions options)
        {
            Options = options;
        }

        public abstract Task ExecuteAsync();
    }
}
#nullable disable
