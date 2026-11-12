// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using TemplateTool;

RootCommand rootCommand = new("Generates Dockerfiles from templates and a ManifestV2 manifest.")
{
    GenerateCommand.Create(),
    ListCommand.Create()
};

return await rootCommand.Parse(args).InvokeAsync();
