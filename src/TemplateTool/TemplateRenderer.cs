// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

/// <summary>
/// The text of a successfully rendered template.
/// </summary>
internal sealed record RenderedTemplate(string Content);

/// <summary>
/// A template that couldn't be read or parsed. It fails the same way regardless of the variables.
/// </summary>
internal sealed record TemplateLoadError(string TemplatePath, string Message);

/// <summary>
/// A template that failed to render with a specific set of variables.
/// </summary>
internal sealed record TemplateRenderError(string TemplatePath, string Message);

internal readonly union RenderResult(RenderedTemplate, TemplateLoadError, TemplateRenderError);

/// <summary>
/// Renders Dockerfile templates with Fluid, loading each template file once.
/// </summary>
/// <param name="baseDirectory">The directory that template paths are relative to.</param>
internal sealed class TemplateRenderer(string baseDirectory)
{
    private static readonly FluidParser Parser = new();

    // Fail on undefined variables and filters instead of rendering empty text or the unfiltered value.
    private static readonly TemplateOptions Options = new() { StrictVariables = true, StrictFilters = true };

    private readonly Dictionary<string, LoadResult> _loadedTemplates = [with(StringComparer.Ordinal)];

    private readonly union LoadResult(IFluidTemplate, TemplateLoadError);

    /// <summary>
    /// Renders the template at <paramref name="templatePath"/>, with each of <paramref name="variables"/> as
    /// a top-level template variable. The output always uses LF line endings.
    /// </summary>
    public async Task<RenderResult> RenderAsync(
        string templatePath,
        Dictionary<string, JsonElement> variables,
        CancellationToken cancellationToken)
    {
        LoadResult loadResult = await LoadTemplateAsync(templatePath, cancellationToken);
        return loadResult switch
        {
            IFluidTemplate template => await RenderTemplateAsync(templatePath, template, variables),
            TemplateLoadError error => error,
        };
    }

    private static async Task<RenderResult> RenderTemplateAsync(
        string templatePath,
        IFluidTemplate template,
        Dictionary<string, JsonElement> variables)
    {
        TemplateContext context = new(Options);
        foreach ((string name, JsonElement value) in variables)
        {
            JsonNode? node = JsonSerializer.SerializeToNode(value);
            context.SetValue(name, node);
        }

        try
        {
            string renderedTemplate = await template.RenderAsync(context);
            return new RenderedTemplate(renderedTemplate.ReplaceLineEndings("\n"));
        }
        catch (FluidException exception)
        {
            return new TemplateRenderError(templatePath, exception.Message);
        }
    }

    private async Task<LoadResult> LoadTemplateAsync(string templatePath, CancellationToken cancellationToken)
    {
        if (_loadedTemplates.TryGetValue(templatePath, out LoadResult cachedResult))
        {
            return cachedResult;
        }

        LoadResult loadResult = await ReadAndParseTemplateAsync(templatePath, cancellationToken);
        _loadedTemplates.Add(templatePath, loadResult);
        return loadResult;
    }

    private async Task<LoadResult> ReadAndParseTemplateAsync(string templatePath, CancellationToken cancellationToken)
    {
        string fullTemplatePath = Path.Join(baseDirectory, templatePath);
        string templateSource;
        try
        {
            templateSource = await File.ReadAllTextAsync(fullTemplatePath, cancellationToken);
        }
        catch (IOException exception)
        {
            return new TemplateLoadError(templatePath, exception.Message);
        }

        if (!Parser.TryParse(templateSource, out IFluidTemplate template, out string error))
        {
            return new TemplateLoadError(templatePath, error);
        }

        return template;
    }
}
