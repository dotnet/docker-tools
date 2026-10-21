// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = ImageBuilder.CreateAppHost();
int exitCode = 1;

try
{
    await host.StartAsync();

    // Some parts of ImageBuilder aren't fully onboarded to DI yet (see StandaloneLoggerFactory).
    // Hand them the host's logger factory so those static code paths can log through the
    // application host's configured logging.
    ILoggerFactory loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
    StandaloneLoggerFactory.LoggerFactory = loggerFactory;

    RootCommand rootCliCommand = [];
    IEnumerable<ICommand> commands = host.Services.GetServices<ICommand>();

    foreach (ICommand command in commands)
    {
        rootCliCommand.Add(command.GetCliCommand());
    }

    ParseResult parseResult = rootCliCommand.Parse(args);
    exitCode = await parseResult.InvokeAsync();
}
catch (Exception e)
{
    ILoggerFactory? loggerFactory = host.Services.GetService<ILoggerFactory>();
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
finally
{
    await host.StopAsync();
}

return exitCode;
