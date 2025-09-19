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

    private readonly IFileSystem _fileSystem = fileSystem;

    private IContext _globalContext = Context.CreateBuiltin(
        new Dictionary<Value, Value>()
        {
            { "replace", ReplaceFunction }
        }
    );

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

    public void AddGlobalVariables(IDictionary<string, string> variables)
    {
        var variableSymbols = new Dictionary<Value, Value>
        {
            { "VARIABLES", variables.ToCottleDictionary() }
        };

        _globalContext = _globalContext.Add(variableSymbols);
    }

    public IContext CreatePlatformContext(PlatformInfo platform)
    {
        var platformVariables = platform.PlatformSpecificTemplateVariables.ToCottleDictionary();

        var variableContext = Context.CreateCustom(platformVariables);
        var platformContext = Context.CreateCascade(primary: variableContext, fallback: _globalContext);

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
                // Resolve arguments to InsertTemplate
                var templateRelativePath = args[0].AsString;
                var templateArgs = args.Count > 1 ? args[1] : Value.EmptyMap;
                var indent = args.Count > 2 ? args[2].AsString : "";

                // Resolve the path of the sub-template to be inserted, relative to the current template
                var parentTemplateDir = Path.GetDirectoryName(currentTemplatePath) ?? string.Empty;
                var newTemplatePath = Path.Combine(parentTemplateDir, templateRelativePath);
                var compiledTemplate = ReadAndCompile(newTemplatePath);

                var newSymbols = new Dictionary<Value, Value>
                {
                    { "InsertTemplate", CreateInsertTemplateFunction(platformContext, newTemplatePath) },
                    { "ARGS", new Dictionary<Value, Value>(templateArgs.Fields) },
                };

                var newContext = platformContext.Add(newSymbols);
                return compiledTemplate.Render(newContext, trim: true, indent: indent);
            }
        );

        return Value.FromFunction(function);
    }

    private static Value ReplaceFunction = Value.FromFunction(
        Function.CreatePure(
            (state, args) =>
            {
                string source = args[0].AsString;
                string oldValue = args[1].AsString;
                string newValue = args[2].AsString;
                return Value.FromString(source.Replace(oldValue, newValue));
            },
            min: 3,
            max: 3
        )
    );
}
