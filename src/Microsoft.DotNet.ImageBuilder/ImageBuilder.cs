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
                    new PublishManifestCommand(),
                    new UpdateReadmeCommand(),
                };

                ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
                {
                    foreach (ICommand command in commands)
                    {
                        command.Options.ParseCommandLine(syntax);
                    }
                });

                if (argSyntax.ActiveCommand != null)
                {
                    ICommand command = commands.Single(c => c.Options == argSyntax.ActiveCommand.Value);
                    command.LoadManifest();
                    command.Execute();
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
