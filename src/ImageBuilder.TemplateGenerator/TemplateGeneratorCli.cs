// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ConsoleAppFramework;

namespace Microsoft.DotNet.ImageBuilder.TemplateGenerator;

internal sealed class TemplateGeneratorCli
{
    [Command("generate-dockerfiles")]
    public async Task GenerateDockerfiles(string manifestPath)
    {
        var manifestJson = await File.ReadAllTextAsync(manifestPath);
    }
}
