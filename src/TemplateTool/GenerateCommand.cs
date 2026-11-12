// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace TemplateTool;

internal static class GenerateCommand
{
    public static Command Create()
    {
        Command command = new("generate", "Generates each platform's Dockerfile from its template.")
        {
            ManifestArgument.Create()
        };

        command.SetAction(parseResult =>
        {
            Console.Error.WriteLine("generate is not implemented yet.");
            return 1;
        });

        return command;
    }
}
