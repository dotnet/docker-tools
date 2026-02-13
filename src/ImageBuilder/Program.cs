// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder;
using Microsoft.DotNet.ImageBuilder.Commands;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

try
{
    RootCommand rootCliCommand = new();

    foreach (ICommand command in ImageBuilder.Commands)
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
    using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
    ILogger logger = loggerFactory.CreateLogger("ImageBuilder.Program");
    logger.LogError(e, "Unhandled exception");
}

return 1;
