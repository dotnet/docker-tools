// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.Extensions.DependencyInjection;

using var host = ImageBuilder.CreateAppHost();

try
{
    await host.StartAsync();

    // Some parts of ImageBuilder aren't fully onboarded to DI yet (see StandaloneLoggerFactory).
    // Hand them the host's logger factory so those static code paths can log through the
    // application host's configured logging.
    var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
    StandaloneLoggerFactory.LoggerFactory = loggerFactory;

    var rootCliCommand = new RootCommand();
    var commands = host.Services.GetServices<ICommand>();

    foreach (var command in commands)
        rootCliCommand.Add(command.GetCliCommand());

    var parseResult = rootCliCommand.Parse(args);
    int exitCode = await parseResult.InvokeAsync();

    await host.StopAsync();
    return exitCode;
}
catch (Exception e)
{
    var loggerFactory = host.Services.GetService<ILoggerFactory>();
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
