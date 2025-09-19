// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cottle;
using Microsoft.DotNet.DockerTools.Templating.Abstractions;
using Microsoft.DotNet.ImageBuilder.ReadModel;

namespace Microsoft.DotNet.DockerTools.Templating.Cottle;

public sealed class CottleTemplateEngine(IFileSystem fileSystem) : ITemplateEngine<IContext>
{
    private static readonly DocumentConfiguration s_config = new()
    {
        BlockBegin = "{{",
        BlockContinue = "^",
        BlockEnd = "}}",
        Escape = '@',
        Trimmer = DocumentConfiguration.TrimNothing
    };

    private static readonly IContext s_globalContext = Context.CreateBuiltin(new Dictionary<Value, Value>());

    private readonly IFileSystem _fileSystem = fileSystem;

    public ICompiledTemplate<IContext> Compile(string template)
    {
        var documentResult = Document.CreateDefault(template, s_config);
        var document = documentResult.DocumentOrThrow;
        var compiledTemplate = new CottleTemplate(document);
        return compiledTemplate;
    }

    public ICompiledTemplate<IContext> ReadAndCompile(string path)
    {
        string content = _fileSystem.ReadAllText(path);
        return Compile(content);
    }

    public IContext CreatePlatformContext(PlatformInfo platform)
    {
        var variables = platform.PlatformSpecificTemplateVariables.ToCottleDictionary();
        var symbols = new Dictionary<Value, Value>
        {
            { "VARIABLES", variables }
        };

        var variableContext = Context.CreateCustom(symbols);
        var platformContext = Context.CreateCascade(primary: variableContext, fallback: s_globalContext);

        // It's OK for the insert template function not to have a reference to itself. Any sub-templates will have
        // their own InsertTemplate function created for them when they are rendered.
        var insertTemplateFunction = CreateInsertTemplateFunction(platformContext, platform.DockerfileTemplatePath!);
        var fullContext = platformContext.Add("InsertTemplate", insertTemplateFunction);

        return fullContext;
    }

    private Value CreateInsertTemplateFunction(IContext platformContext, string currentTemplatePath)
    {
        var function = Function.CreatePure(
            (state, args) =>
            {
                var templateRelativePath = args[0].AsString;
                var templateArgs = args.Count > 1 ? args[1] : Value.EmptyMap;
                var indent = args.Count > 2 ? args[2].AsString : "";

                var parentTemplateDir = Path.GetDirectoryName(currentTemplatePath) ?? string.Empty;
                var newTemplatePath = Path.Combine(parentTemplateDir, templateRelativePath);
                var compiledTemplate = ReadAndCompile(newTemplatePath);

                var newInsertTemplateFunction = CreateInsertTemplateFunction(platformContext, newTemplatePath);
                var newContext = platformContext.Add("InsertTemplate", newInsertTemplateFunction);

                return compiledTemplate.Render(newContext);
            }
        );

        return Value.FromFunction(function);
    }
}
