// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

/// <summary>
/// Loads and renders Dockerfile templates with Fluid, loading each template file once.
/// </summary>
internal sealed class TemplateRenderer
{
    private static readonly FluidParser Parser = new();

    // Fail on undefined variables and filters instead of rendering empty text or the unfiltered value.
    private static readonly TemplateOptions Options = new() { StrictVariables = true, StrictFilters = true };

    private readonly Dictionary<string, Result<IFluidTemplate, string>> _loadedTemplates = [with(StringComparer.Ordinal)];

    /// <summary>
    /// Reads and parses the template at <paramref name="templatePath"/>.
    /// </summary>
    public Result<IFluidTemplate, string> Load(string templatePath)
    {
        if (_loadedTemplates.TryGetValue(templatePath, out Result<IFluidTemplate, string> cachedResult))
        {
            return cachedResult;
        }

        Result<IFluidTemplate, string> loadResult = ReadAndParse(templatePath);
        _loadedTemplates.Add(templatePath, loadResult);
        return loadResult;
    }

    /// <summary>
    /// Renders <paramref name="template"/> with each of <paramref name="variables"/> as a top-level template
    /// variable. The output always uses LF line endings.
    /// </summary>
    public static Result<string, string> Render(IFluidTemplate template, Dictionary<string, JsonElement> variables)
    {
        TemplateContext context = new(Options);
        foreach ((string name, JsonElement value) in variables)
        {
            JsonNode? node = JsonSerializer.SerializeToNode(value);
            context.SetValue(name, node);
        }

        try
        {
            string renderedTemplate = template.Render(context);
            return new Ok<string>(renderedTemplate.ReplaceLineEndings("\n"));
        }
        catch (FluidException exception)
        {
            return new Error<string>(exception.Message);
        }
    }

    private static Result<IFluidTemplate, string> ReadAndParse(string templatePath)
    {
        string templateSource;
        try
        {
            templateSource = File.ReadAllText(templatePath);
        }
        catch (IOException exception)
        {
            return new Error<string>(exception.Message);
        }

        if (!Parser.TryParse(templateSource, out IFluidTemplate template, out string error))
        {
            return new Error<string>(error);
        }

        return new Ok<IFluidTemplate>(template);
    }
}
