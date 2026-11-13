// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

/// <summary>
/// Renders Dockerfile templates with Fluid, parsing each template file once.
/// </summary>
/// <param name="baseDirectory">The directory that template paths are relative to.</param>
internal sealed class TemplateRenderer(string baseDirectory)
{
    private static readonly FluidParser Parser = new();

    // Fail on undefined variables instead of rendering them as empty text.
    private static readonly TemplateOptions Options = new() { StrictVariables = true };

    private readonly Dictionary<string, IFluidTemplate> _parsedTemplates = new(StringComparer.Ordinal);

    /// <summary>
    /// Renders the template at <paramref name="templatePath"/>, with each of <paramref name="variables"/> as
    /// a top-level template variable. The output always uses LF line endings.
    /// </summary>
    public async Task<string> RenderAsync(
        string templatePath, Dictionary<string, JsonElement> variables, CancellationToken cancellationToken)
    {
        IFluidTemplate template = await GetTemplateAsync(templatePath, cancellationToken);
        TemplateContext context = new(Options);
        foreach ((string name, JsonElement value) in variables)
        {
            JsonNode? node = JsonSerializer.SerializeToNode(value);
            context.SetValue(name, node);
        }

        string renderedTemplate = await template.RenderAsync(context);
        return renderedTemplate.ReplaceLineEndings("\n");
    }

    private async Task<IFluidTemplate> GetTemplateAsync(string templatePath, CancellationToken cancellationToken)
    {
        if (_parsedTemplates.TryGetValue(templatePath, out IFluidTemplate? template))
        {
            return template;
        }

        string fullTemplatePath = Path.Join(baseDirectory, templatePath);
        string templateSource = await File.ReadAllTextAsync(fullTemplatePath, cancellationToken);
        if (!Parser.TryParse(templateSource, out template, out string error))
        {
            throw new InvalidDataException($"{templatePath}: {error}");
        }

        _parsedTemplates.Add(templatePath, template);
        return template;
    }
}
