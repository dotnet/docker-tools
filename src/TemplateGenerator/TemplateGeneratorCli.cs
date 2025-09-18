// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ConsoleAppFramework;
using Microsoft.DotNet.ImageBuilder.ReadModel;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator;

internal sealed class TemplateGeneratorCli
{
    /// <summary>
    /// Generates Dockerfiles from a manifest file.
    /// </summary>
    /// <param name="manifestPath">Path to manifest JSON file</param>
    [Command("generate-dockerfiles")]
    public async Task GenerateDockerfiles([Argument] string manifestPath)
    {
        ManifestInfo manifest = await ManifestInfo.LoadAsync(manifestPath);
        var manifestString = manifest.ToJsonString();
        await File.WriteAllTextAsync("manifest.processed.json", manifestString);
    }
}
