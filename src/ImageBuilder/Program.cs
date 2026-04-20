// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.Extensions.DependencyInjection;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

try
{
    RootCommand rootCliCommand = new();

    foreach (ICommand command in ImageBuilder.Commands)
    {
        rootCliCommand.Add(command.GetCliCommand());
    }

    return await rootCliCommand.Parse(args).InvokeAsync();
}
catch (Exception e)
{
    ILoggerFactory? loggerFactory = ImageBuilder.Services.GetService<ILoggerFactory>();
    if (loggerFactory is not null)
    {
        ILogger logger = loggerFactory.CreateLogger("ImageBuilder.Program");
        logger.LogError(e, "Unhandled exception");
    }
    else
    {
        Console.Error.WriteLine(e);
    }
}

return 1;
