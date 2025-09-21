// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ConsoleAppFramework;
using Microsoft.DotNet.ImageBuilder.ReadModel;
using Microsoft.DotNet.ImageBuilder.ReadModel.Serialization;
using Microsoft.DotNet.DockerTools.Templating.Cottle;
using Microsoft.DotNet.DockerTools.Templating;
using System.Diagnostics;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator;

public sealed class TemplateGeneratorCli
{
    /// <summary>
    /// Generates Dockerfiles from a manifest file.
    /// </summary>
    /// <param name="manifestPath">Path to manifest JSON file</param>
    public void GenerateDockerfiles([Argument] string manifestPath)
    {
        var manifest = ManifestInfo.Load(manifestPath);

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
            var platformContext = engine.CreateContext(
                variables: platform.PlatformSpecificTemplateVariables,
                // Null-forgiving operator is safe here because we filtered out
                // platforms without templates above.
                templatePath: platform.DockerfileTemplatePath!);

            var output = compiledTemplate.Render(platformContext);
            fileSystem.WriteAllText(platform.DockerfilePath, output);
        }

        fileSystem.LogStatistics();
        fileSystemCache.LogStatistics();
        engine.LogStatistics();
    }

    /// <summary>
    /// Generates README.md files from a manifest file.
    /// </summary>
    /// <param name="manifestPath">Path to manifest JSON file</param>
    public void GenerateReadmes([Argument] string manifestPath)
    {
        var manifest = ManifestInfo.Load(manifestPath);

        var fileSystem = new FileSystem();
        var fileSystemCache = new FileSystemCache(fileSystem);
        var engine = new CottleTemplateEngine(fileSystemCache);
        engine.AddGlobalVariables(manifest.Model.Variables);

        var templatedRepoReadmes =
            manifest.Repos
                .SelectMany(repo => repo.Readmes
                    .Where(readme => readme.TemplatePath is not null)
                    .Select(readme => (Repo: repo, Readme: readme)));

        foreach (var (repo, readme) in templatedRepoReadmes)
        {
            // Null-forgiving operator is safe here because we filtered out
            // readmes without templates above.
            var readmeTemplatePath = readme.TemplatePath!;
            var compiledTemplate = engine.ReadAndCompile(readmeTemplatePath);
            var repoContext = engine.CreateContext(repo.TemplateVariables, readmeTemplatePath);
            var output = compiledTemplate.Render(repoContext);
            fileSystem.WriteAllText(readme.FilePath, output);
        }

        var manifestReadme = manifest.Readme;
        if (manifestReadme is not null && manifestReadme.TemplatePath is not null)
        {
            var compiledTemplate = engine.ReadAndCompile(manifestReadme.TemplatePath);
            var manifestContext = engine.CreateContext(manifest.TemplateVariables, manifestReadme.TemplatePath);
            var output = compiledTemplate.Render(manifestContext);
            fileSystem.WriteAllText(manifestReadme.FilePath, output);
        }

        fileSystem.LogStatistics();
        fileSystemCache.LogStatistics();
        engine.LogStatistics();
    }

    /// <summary>
    /// Generates both Dockerfiles and READMEs from a manifest file.
    /// </summary>
    /// <param name="manifestPath">Path to manifest JSON file</param>
    public void GenerateAll([Argument] string manifestPath)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        Console.WriteLine(
            """

            --- Generating Dockerfiles ---
            """);
        GenerateDockerfiles(manifestPath);

        stopwatch.Stop();

        Console.WriteLine(
            $"""
            ({stopwatch.ElapsedMilliseconds} ms)

            --- Generating READMEs ---
            """);

        stopwatch.Reset();
        stopwatch.Start();

        GenerateReadmes(manifestPath);

        Console.WriteLine(
            $"""
            ({stopwatch.ElapsedMilliseconds} ms)

            """
        );
    }
}
