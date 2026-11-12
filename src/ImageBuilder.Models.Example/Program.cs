// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:project ..\ImageBuilder.Models\Microsoft.DotNet.ImageBuilder.Models.csproj
#:property PublishAot=false

using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

// Run from this directory: dotnet run --file Program.cs [manifest path]
// Reads and resolves a manifest, which validates it.
if (args.Length > 1)
{
    throw new ArgumentException("Expected at most one argument: the manifest path.");
}

string inputPath = args.Length == 1 ? args[0] : "manifest.yaml";

Manifest resolved = await ManifestLoader.LoadAsync(inputPath);
Console.WriteLine(
    $"Resolved {inputPath}: {resolved.GetImages().Count()} images, {resolved.GetPlatforms().Count()} platforms");
