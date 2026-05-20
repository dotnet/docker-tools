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

            Command cmd = new Command(this.GetCommandName(), Description);
            cmd.AddOptions(options);

            cmd.SetAction(async (parseResult, cancellationToken) =>
            {
                try
                {
                    options.Bind(parseResult);

                    if (!options.NoVersionLogging)
                    {
                        LogDockerVersions();
                    }

                    Initialize(options);
                    await ExecuteAsync();
                }
                catch (Exception ex)
                {
                    // System.CommandLine's DefaultExceptionHandler silently swallows
                    // OperationCanceledException (and AggregateException wrapping one),
                    // producing exit code 1 with no diagnostic output. Write the
                    // exception details to stderr ourselves before rethrowing so
                    // failures are always observable in pipeline logs.
                    // See: https://github.com/dotnet/command-line-api/issues/430
                    Console.Error.WriteLine($"Unhandled exception in command '{this.GetCommandName()}':");
                    Console.Error.WriteLine(ex.ToString());
                    Console.Error.Flush();
                    throw;
                }
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
