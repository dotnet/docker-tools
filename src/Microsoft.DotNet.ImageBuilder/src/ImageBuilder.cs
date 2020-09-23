// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageBuilder
    {
        private static CompositionContainer s_container;

        public static CompositionContainer Container
        {
            get
            {
                if (s_container == null)
                {
                    string dllLocation = Assembly.GetExecutingAssembly().Location;
                    DirectoryCatalog catalog = new DirectoryCatalog(Path.GetDirectoryName(dllLocation), Path.GetFileName(dllLocation));
                    s_container = new CompositionContainer(catalog);
                }

                return s_container;
            }
        }

        public static int Main(string[] args)
        {
            int result = 0;

            try
            {
                ICommand[] commands = Container.GetExportedValues<ICommand>().ToArray();

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
                        Logger.WriteError($"error: `{arg.Name}` must be specified.");
                        Environment.Exit(1);
                    }
                }

                if (argSyntax.ActiveCommand != null)
                {
                    // Capture the Docker version and info in the output.
                    ExecuteHelper.Execute(fileName: "docker", args: "version", isDryRun: false);
                    ExecuteHelper.Execute(fileName: "docker", args: "info", isDryRun: false);

                    ICommand command = commands.Single(c => c.Options == argSyntax.ActiveCommand.Value);
                    if (command is IManifestCommand manifestCommand)
                    {
                        manifestCommand.LoadManifest();
                    }
                    
                    command.ExecuteAsync().Wait();
                }
            }
            catch (Exception e)
            {
                Logger.WriteError(e.ToString());

                result = 1;
            }

            return result;
        }
    }
}
