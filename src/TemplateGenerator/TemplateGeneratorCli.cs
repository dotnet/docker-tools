// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ConsoleAppFramework;
using Microsoft.DotNet.ImageBuilder.ReadModel;
using Microsoft.DotNet.ImageBuilder.ReadModel.Serialization;
using Microsoft.DotNet.DockerTools.Templating.Cottle;

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

        var engine = new CottleTemplateEngine();
        var globalContext = engine.CreateContext(new Dictionary<string, string>());

        var platformsWithTemplates = manifest.AllPlatforms
            .Where(platform => platform.DockerfileTemplatePath is not null);

        var compiledTemplates = platformsWithTemplates
            .Select(platform => platform.DockerfileTemplatePath!)
            .Select(File.ReadAllText)
            .Select(engine.Compile);

        var compiledTemplateInfos = platformsWithTemplates
            .Zip(compiledTemplates);

        foreach (var (platform, compiledTemplate) in compiledTemplateInfos)
        {
            var platformSpecificContext = globalContext.Add(platform.PlatformSpecificTemplateVariables);
            var output = compiledTemplate.Render(platformSpecificContext);
            File.WriteAllText(platform.DockerfilePath, output);
        }
    }
}

public static class TemplateGenerator
{
    public static string GenerateCottleTemplate()
    {
        const string TemplateString =
            """
            FROM Repo:2.1-{{OS_VERSION_BASE}}
            ENV TEST1 {{if OS_VERSION = "trixie-slim":IfWorks}}
            ENV TEST2 {{VARIABLES["Variable1"]}}
            """;

        // Expected output:
        // FROM Repo:2.1-trixie
        // ENV TEST1 IfWorks
        // ENV TEST2 Value1

        var engine = new CottleTemplateEngine();

        var predefinedVariables = new Dictionary<string, string>()
        {
            { "Variable1", "Value1" }
        };

        var contextBasedVariables = new Dictionary<string, string>()
        {
            { "OS_VERSION", "trixie-slim" },
            { "OS_VERSION_BASE", "trixie" },
        };

        var templateContext = engine.CreateVariableContext(predefinedVariables);
        templateContext.Add(contextBasedVariables);

        var template = engine.Compile(TemplateString);
        string result = template.Render(templateContext);
        return result;
    }
}
