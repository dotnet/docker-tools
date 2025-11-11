// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cottle;
using Microsoft.DotNet.DockerTools.Templating.Abstractions;

namespace Microsoft.DotNet.DockerTools.Templating.Cottle;

public sealed class CottleTemplateEngine : ITemplateEngine<IContext>
{
    private static readonly DocumentConfiguration s_config = new()
    {
        BlockBegin = "{{",
        BlockContinue = "^",
        BlockEnd = "}}",
        Escape = '@',
        Trimmer = DocumentConfiguration.TrimNothing
    };

    private readonly IFileSystem _fileSystem;
    private readonly ForeverCache<CottleTemplate> _templateCache;

    private IContext _globalContext = Context.CreateBuiltin(
        new Dictionary<Value, Value>()
        {
            { "replace", ReplaceFunction }
        }
    );

    public CottleTemplateEngine(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _templateCache = new ForeverCache<CottleTemplate>(valueFactory: ReadAndCompileWithNoCache);
    }

    public int CompiledTemplateCacheHits => _templateCache.Hits;
    public int CompiledTemplateCacheMisses => _templateCache.Misses;

    public ICompiledTemplate<IContext> ReadAndCompile(string path)
    {
        return _templateCache.GetOrAdd(path);
    }

    public void AddGlobalVariables(IDictionary<string, string> variables)
    {
        var variableSymbols = new Dictionary<Value, Value>
        {
            { "VARIABLES", variables.ToCottleDictionary() }
        };

        _globalContext = _globalContext.Add(variableSymbols);
    }

    /// <inheritdoc/>
    public IContext CreateContext(IDictionary<string, string> variables, string templatePath)
    {
        var variableSymbols = variables.ToCottleDictionary();
        var variableContext = Context.CreateCustom(variableSymbols);
        var newContext = Context.CreateCascade(primary: variableContext, fallback: _globalContext);

        // It's OK for the insert template function not to have a reference to itself. Any sub-templates will have
        // their own InsertTemplate function created for them when they are rendered.
        var insertTemplateFunction = CreateInsertTemplateFunction(newContext, templatePath);
        newContext = newContext.Add("InsertTemplate", insertTemplateFunction);

        return newContext;
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

    private CottleTemplate ReadAndCompileWithNoCache(string path)
    {
        string content = _fileSystem.ReadAllText(path);
        var documentResult = Document.CreateDefault(content, s_config);
        var document = documentResult.DocumentOrThrow;
        var compiledTemplate = new CottleTemplate(document);
        return compiledTemplate;
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
