// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

IHost host = ImageBuilder.AppHost;
try
{
    await host.StartAsync();

    RootCommand rootCliCommand = new();

    foreach (ICommand command in ImageBuilder.Commands)
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
    if (host is IAsyncDisposable asyncDisposableHost)
    {
        await asyncDisposableHost.DisposeAsync();
    }
    else
    {
        host.Dispose();
    }
}

return 1;
