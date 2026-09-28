// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public static class CommandAction
{
    /// <summary>
    /// Creates a CLI command that binds <paramref name="options"/> from the parse result and then invokes
    /// <paramref name="run"/>.
    /// </summary>
    public static Command Create(string name, string description, Options options, Func<CancellationToken, Task> run)
    {
        Command command = new(name, description);
        command.AddOptions(options);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                options.Bind(parseResult);

                if (!options.NoVersionLogging)
                {
                    LogDockerVersions(cancellationToken);
                }

                await run(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // System.CommandLine silently swallows OperationCanceledException and TaskCanceledException, so
                // log all unhandled exceptions to stderr here and re-throw. This makes sure failures are always
                // observable in pipeline logs.
                // For more details, see https://github.com/dotnet/command-line-api/issues/2808.
                Console.Error.WriteLine($"Unhandled exception in command '{name}':");
                Console.Error.WriteLine(ex.ToString());
                Console.Error.Flush();
                throw;
            }
        });

        return command;
    }

    private static void LogDockerVersions(CancellationToken cancellationToken)
    {
        // Capture the Docker version and info in the output.
        ExecuteHelper.Execute(fileName: "docker", args: "version", isDryRun: false, cancellationToken);
        ExecuteHelper.Execute(fileName: "docker", args: "info", isDryRun: false, cancellationToken);
    }
}
