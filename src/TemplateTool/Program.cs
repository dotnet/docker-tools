// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

Argument<FileInfo> manifestArgument = new("manifest")
{
    Description = "The manifest. Template and Dockerfile paths are relative to its directory."
};

RootCommand rootCommand = new("Generates Dockerfiles from templates and a ManifestV2 manifest.")
{
    manifestArgument
};

rootCommand.SetAction(parseResult =>
{
    Console.Error.WriteLine("TemplateTool is not implemented yet.");
    return 1;
});

return await rootCommand.Parse(args).InvokeAsync();
