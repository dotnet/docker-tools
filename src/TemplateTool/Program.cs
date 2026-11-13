// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.TemplateTool;

RootCommand rootCommand =
[
    with("Generates Dockerfiles from templates and a ManifestV2 manifest."),
    GenerateCommand.Create(),
    ListCommand.Create()
];

ParseResult parseResult = rootCommand.Parse(args);
return parseResult.Invoke();
