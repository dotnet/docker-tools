// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Commands;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

namespace Microsoft.DotNet.ImageBuilder;

public static class ImageBuilder
{
    private static CompositionContainer s_container;

    private static CompositionContainer Container
    {
        get
        {
            if (s_container == null)
            {
                string dllLocation = Assembly.GetExecutingAssembly().Location;
                DirectoryCatalog catalog = new(Path.GetDirectoryName(dllLocation), Path.GetFileName(dllLocation));
                s_container = new CompositionContainer(catalog, CompositionOptions.DisableSilentRejection);
            }

            return s_container;
        }
    }

    public static ICommand[] Commands => Container.GetExportedValues<ICommand>().ToArray();

    public static int Main(string[] args)
    {
        int result = 0;

        try
        {
            RootCommand rootCliCommand = new();

            foreach (ICommand command in Commands)
            {
                rootCliCommand.AddCommand(command.GetCliCommand());
            }

            Parser parser = new CommandLineBuilder(rootCliCommand)
                .UseDefaults()
                .UseMiddleware(context =>
                {
                    context.BindingContext.AddModelBinder(new ModelBinder<AzdoOptions>());
                    context.BindingContext.AddModelBinder(new ModelBinder<GitOptions>());
                    context.BindingContext.AddModelBinder(new ModelBinder<ManifestFilterOptions>());
                    context.BindingContext.AddModelBinder(new ModelBinder<RegistryCredentialsOptions>());
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
