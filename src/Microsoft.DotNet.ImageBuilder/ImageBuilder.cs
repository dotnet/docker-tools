// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.CommandLine;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageBuilder
    {
        public static int Main(string[] args)
        {
            int result = 0;

            try
            {
                ICommand[] commands = {
                    new BuildCommand(),
                    new GenerateTagsReadmeCommand(),
                    new PublishManifestCommand(),
                    new UpdateReadmeCommand(),
                    new UpdateVersionsCommand(),
                };

                ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
                {
                    foreach (ICommand command in commands)
                    {
                        command.Options.ParseCommandLine(syntax);
                    }
                });

                // Workaround for https://github.com/dotnet/corefxlab/issues/1689
                foreach (Argument arg in argSyntax.GetActiveArguments())
                {
                    if (arg.IsParameter && !arg.IsSpecified)
                    {
                        Console.Error.WriteLine($"error: `{arg.Name}` must be specified.");
                        Environment.Exit(1);
                    }
                }

                if (argSyntax.ActiveCommand != null)
                {
                    ExecuteHelper.ExecuteWithRetry("docker", "version", false);
                    ICommand command = commands.Single(c => c.Options == argSyntax.ActiveCommand.Value);
                    command.LoadManifest();
                    command.ExecuteAsync().Wait();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                result = 1;
            }

            return result;
        }
    }
}
