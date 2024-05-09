// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Commands;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

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

                RootCommand rootCliCommand = new RootCommand();

                foreach (ICommand command in commands)
                {
                    rootCliCommand.AddCommand(command.GetCliCommand());
                }

                Parser parser = new CommandLineBuilder(rootCliCommand)
                    .UseDefaults()
                    .UseMiddleware(context =>
                    {
                        if (context.ParseResult.CommandResult.Command != rootCliCommand)
                        {
                            // Capture the Docker version and info in the output.
                            ExecuteHelper.Execute(fileName: "docker", args: "version", isDryRun: false);
                            ExecuteHelper.Execute(fileName: "docker", args: "info", isDryRun: false);
                        }
                    })
                    .UseMiddleware(context =>
                    {
                        context.BindingContext.AddModelBinder(new ModelBinder<AzdoOptions>());
                        context.BindingContext.AddModelBinder(new ModelBinder<GitOptions>());
                        context.BindingContext.AddModelBinder(new ModelBinder<ManifestFilterOptions>());
                        context.BindingContext.AddModelBinder(new ModelBinder<RegistryCredentialsOptions>());
                        context.BindingContext.AddModelBinder(new ModelBinder<ServicePrincipalOptions>());
                        context.BindingContext.AddModelBinder(new ModelBinder<SubscriptionOptions>());
                    })
                    .Build();
                return parser.Invoke(args);
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
