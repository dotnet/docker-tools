// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        protected IDockerService DockerService { get; }

        public Command(IDockerService dockerService)
        {
            Options = new TOptions();
            DockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
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
                Initialize(options);
                return ExecuteAsync();

                
            });

            return cmd;
        }

        protected virtual void Initialize(TOptions options)
        {
            Options = options;
        }

        public async Task ExecuteAsync()
        {
            if (Options is IDockerCredsOptionsHost dockerCredsOptionsHost)
            {
                DockerService.IsAnonymousAccessAllowed = dockerCredsOptionsHost.DockerCredsOptions.AllowAnonymousAccess;

                if (dockerCredsOptionsHost.DockerCredsOptions.DockerUsername is not null)
                {
                    await DockerService.ExecuteWithUserAsync(
                        ExecuteCoreAsync,
                        dockerCredsOptionsHost.DockerCredsOptions.DockerUsername,
                        dockerCredsOptionsHost.DockerCredsOptions.DockerPassword,
                        server: null,
                        isDryRun: Options.IsDryRun);
                    return;
                }
            }

            await ExecuteCoreAsync();
        }

        protected abstract Task ExecuteCoreAsync();
    }
}
#nullable disable
