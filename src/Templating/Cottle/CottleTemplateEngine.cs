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
    private readonly ForeverCache<ICompiledTemplate<IContext>> _templateCache;

    private IContext _globalContext = Context.CreateBuiltin(
        new Dictionary<Value, Value>()
        {
            { "replace", ReplaceFunction }
        }
    );

    public CottleTemplateEngine(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _templateCache = new ForeverCache<ICompiledTemplate<IContext>>(ReadAndCompileWithNoCache);
    }

    public int CompiledTemplateCacheHits => _templateCache.Hits;
    public int CompiledTemplateCacheMisses => _templateCache.Misses;

    public ICompiledTemplate<IContext> Compile(string template)
    {
        var documentResult = Document.CreateDefault(template, s_config);
        var document = documentResult.DocumentOrThrow;
        var compiledTemplate = new CottleTemplate(document);
        return compiledTemplate;
    }

    public ICompiledTemplate<IContext> ReadAndCompile(string path)
    {
        return _templateCache.GetOrAdd(path);
    }

    private ICompiledTemplate<IContext> ReadAndCompileWithNoCache(string path)
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

    /// <summary>
    /// Create a new context for rendering a template.
    /// </summary>
    /// <param name="variables">
    /// Dictionary of variables to add to context. These variables will take
    /// precedence over any global variables already set in the engine.
    /// </param>
    /// <param name="templatePath">
    /// The path to the current template is needed to correctly resolve paths
    /// to sub-templates
    /// </param>
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
