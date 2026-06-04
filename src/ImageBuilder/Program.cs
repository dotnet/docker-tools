// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

using IHost host = ImageBuilder.CreateAppHost();

// Some parts of ImageBuilder aren't fully onboarded to DI yet (see StandaloneLoggerFactory).
// Hand them the host's logger factory so those static code paths can log through the
// application host's configured logging.
StandaloneLoggerFactory.LoggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
try
{
    await host.StartAsync();

    RootCommand rootCliCommand = new();

    foreach (ICommand command in host.Services.GetServices<ICommand>())
    {
        rootCliCommand.Add(command.GetCliCommand());
    }

    int exitCode = await rootCliCommand.Parse(args).InvokeAsync();

    await host.StopAsync();
    return exitCode;
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
    // Drop the reference to the host-owned factory so static logging falls back to a no-op
    // once the host is disposed.
    StandaloneLoggerFactory.LoggerFactory = NullLoggerFactory.Instance;
}

return 1;
