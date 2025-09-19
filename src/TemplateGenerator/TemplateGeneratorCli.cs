// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ConsoleAppFramework;
using Microsoft.DotNet.ImageBuilder.ReadModel;
using Microsoft.DotNet.ImageBuilder.ReadModel.Serialization;
using Microsoft.DotNet.DockerTools.Templating.Cottle;
using Microsoft.DotNet.DockerTools.Templating;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator;

public sealed class TemplateGeneratorCli
{
    /// <summary>
    /// Generates Dockerfiles from a manifest file.
    /// </summary>
    /// <param name="manifestPath">Path to manifest JSON file</param>
    [Command("generate-dockerfiles")]
    public async Task GenerateDockerfiles([Argument] string manifestPath)
    {
        ManifestInfo manifest = await ManifestInfo.LoadAsync(manifestPath);

        var fileSystem = new FileSystem();
        var fileSystemCache = new FileSystemCache(fileSystem);
        var engine = new CottleTemplateEngine(fileSystemCache);
        engine.AddGlobalVariables(manifest.Model.Variables);

        var platformsWithTemplates = manifest.AllPlatforms
            .Where(platform => platform.DockerfileTemplatePath is not null);

        var compiledTemplates = platformsWithTemplates
            .Select(platform => platform.DockerfileTemplatePath!)
            .Select(engine.ReadAndCompile);

        var compiledTemplateInfos = platformsWithTemplates
            .Zip(compiledTemplates);

        foreach (var (platform, compiledTemplate) in compiledTemplateInfos)
        {
            var platformContext = engine.CreatePlatformContext(platform);
            var output = compiledTemplate.Render(platformContext);
            fileSystem.WriteAllText(platform.DockerfilePath, output);
        }

        Console.WriteLine($"Read {fileSystem.FilesRead} files ({fileSystem.BytesRead} bytes)");
        Console.WriteLine($"Wrote {fileSystem.FilesWritten} files ({fileSystem.BytesWritten} bytes)");
        Console.WriteLine($"File system cache hits: {fileSystemCache.CacheHits}, misses: {fileSystemCache.CacheMisses}");
        Console.WriteLine($"Compiled template cache hits: {engine.CompiledTemplateCacheHits}, misses: {engine.CompiledTemplateCacheMisses}");
    }
}
