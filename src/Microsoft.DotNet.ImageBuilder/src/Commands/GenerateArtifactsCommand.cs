// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cottle;
using Cottle.Exceptions;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public delegate (IReadOnlyDictionary<Value, Value> Symbols, string Indent) GetTemplateState<TContext>(
        TContext context, string templatePath, string indent);

    public abstract class GenerateArtifactsCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
        where TOptions : GenerateArtifactsOptions, new()
        where TOptionsBuilder : GenerateArtifactsOptionsBuilder, new()
    {
        private readonly DocumentConfiguration _config = new DocumentConfiguration
        {
            BlockBegin = "{{",
            BlockContinue = "^",
            BlockEnd = "}}",
            Escape = '@',
            Trimmer = DocumentConfiguration.TrimNothing
        };

        private readonly IEnvironmentService _environmentService;
        private readonly List<string> _invalidTemplates = new List<string>();
        private readonly List<string> _outOfSyncArtifacts = new List<string>();
        private readonly Dictionary<string, string> generatedArtifacts = new();

        protected GenerateArtifactsCommand(IEnvironmentService environmentService) : base()
        {
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        protected async Task GenerateArtifactsAsync<TContext>(
            IEnumerable<TContext> contexts,
            Func<TContext, string> getTemplatePath,
            Func<TContext, string> getArtifactPath,
            GetTemplateState<TContext> getState,
            string templatePropertyName,
            string artifactName,
            Func<string, TContext, string> postProcess = null)
        {
            foreach (TContext context in contexts)
            {
                string artifactPath = getArtifactPath(context);
                if (artifactPath == null)
                {
                    continue;
                }

                string templatePath = getTemplatePath(context);
                if (templatePath == null)
                {
                    if (Options.AllowOptionalTemplates)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"The {artifactName} `{artifactPath}` does not have a {templatePropertyName} specified.");
                }

                // There may be some artifact files which are referenced more than once in different contexts. Since we can only
                // generate the artifact once, we take the approach of first-one-wins. Once an artifact has been generated once,
                // it doesn't get generated again during the running of this command.
                if (generatedArtifacts.TryGetValue(artifactPath, out string originalTemplatePath))
                {
                    if (originalTemplatePath != templatePath)
                    {
                        throw new InvalidOperationException(
                            $"Multiple unique template files are associated with the generated artifact path '{artifactPath}':" + Environment.NewLine +
                            originalTemplatePath + Environment.NewLine + templatePath);
                    }

                    continue;
                }

                await GenerateArtifactAsync(templatePath, artifactPath, context, getState, artifactName, postProcess);
                generatedArtifacts.Add(artifactPath, templatePath);
            }
        }

        private async Task GenerateArtifactAsync<TContext>(
            string templatePath,
            string artifactPath,
            TContext context,
            GetTemplateState<TContext> getState,
            string artifactName,
            Func<string, TContext, string> postProcess)
        {
            Logger.WriteSubheading($"Generating '{artifactPath}' from '{templatePath}'");

            string generatedArtifact = await RenderTemplateAsync(templatePath, context, getState, Value.EmptyMap, null, trimTemplate: false);

            if (generatedArtifact != null)
            {
                if (postProcess != null)
                {
                    generatedArtifact = postProcess(generatedArtifact, context);
                }

                string currentArtifact = File.Exists(artifactPath) ?
                    await File.ReadAllTextAsync(artifactPath) : string.Empty;
                if (currentArtifact == generatedArtifact)
                {
                    Logger.WriteMessage($"{artifactName} in sync with template");
                }
                else if (Options.Validate)
                {
                    int differIndex = StringExtensions.DiffersAtIndex(currentArtifact, generatedArtifact);
                    Logger.WriteError($"{artifactName} out of sync with template starting at index '{differIndex}'{Environment.NewLine}"
                        + $"Current:   '{GetSnippet(currentArtifact, differIndex)}'{Environment.NewLine}"
                        + $"Generated: '{GetSnippet(generatedArtifact, differIndex)}'");
                    _outOfSyncArtifacts.Add(artifactPath);
                }
                else if (!Options.IsDryRun)
                {
                    await File.WriteAllTextAsync(artifactPath, generatedArtifact);
                    Logger.WriteMessage($"Updated '{artifactPath}'");
                }
            }
        }

        private static string GetSnippet(string source, int index) => source.Substring(index, Math.Min(100, source.Length - index));

        protected Dictionary<Value, Value> GetSymbols<TContext>(
            string sourceTemplatePath,
            TContext context,
            GetTemplateState<TContext> getTemplateState,
            string indent)
        {
            return new Dictionary<Value, Value>
            {
                ["VARIABLES"] = Manifest.VariableHelper.ResolvedVariables
                    .ToDictionary(kvp => (Value)kvp.Key, kvp => (Value)kvp.Value),
                ["InsertTemplate"] = Value.FromFunction(
                    Function.CreatePure(
                        (state, args) =>
                            RenderTemplateAsync(
                                Path.Combine(Path.GetDirectoryName(sourceTemplatePath), args[0].AsString),
                                context,
                                getTemplateState,
                                args.Count > 1 ? args[1] : Value.EmptyMap,
                                args.Count > 2 ? args[2].AsString : null,
                                trimTemplate: true).Result,
                        min: 1,
                        max: 3))
            };
        }

        protected async Task<string> RenderTemplateAsync<TContext>(
            string templatePath,
            TContext context,
            GetTemplateState<TContext> getTemplateState,
            Value templateArgs,
            string currentIndent,
            bool trimTemplate)
        {
            string artifact = null;

            string template = await File.ReadAllTextAsync(templatePath);

            if (trimTemplate)
            {
                template = template.Trim();
            }

            // Indents for nested templates are cumulative. Pass the current indent value and the result will contain
            // the new indent value to use for the nested template.
            (IReadOnlyDictionary<Value, Value> Symbols, string Indent) state = getTemplateState(context, templatePath, currentIndent);
            IReadOnlyDictionary<Value, Value> symbols = new Dictionary<Value, Value>(state.Symbols)
            {
                { "ARGS", new Dictionary<Value, Value>(templateArgs.Fields) }
            };          

            if (!string.IsNullOrEmpty(state.Indent))
            {
                // Indents all the lines except the first one
                template = template.Replace("\n", $"\n{state.Indent}");
            }

            if (Options.IsVerbose)
            {
                Logger.WriteMessage($"Template:{Environment.NewLine}{template}");
            }

            try
            {
                IDocument document = Document.CreateDefault(template, _config).DocumentOrThrow;                
                artifact = document.Render(Context.CreateBuiltin(symbols));

                if (Options.IsVerbose)
                {
                    Logger.WriteMessage($"Generated:{Environment.NewLine}{artifact}");
                }
            }
            catch (ParseException e)
            {
                Logger.WriteError($"Error: {e}{Environment.NewLine}Invalid Syntax:{Environment.NewLine}{template.Substring(e.LocationStart)}");
                _invalidTemplates.Add(templatePath);
            }

            return artifact;
        }

        protected void ValidateArtifacts()
        {
            if (_outOfSyncArtifacts.Any() || _invalidTemplates.Any())
            {
                if (_outOfSyncArtifacts.Any())
                {
                    string artifacts = string.Join(Environment.NewLine, _outOfSyncArtifacts);
                    Logger.WriteError($"Out of sync with templates:{Environment.NewLine}{artifacts}");
                }

                if (_invalidTemplates.Any())
                {
                    string templateList = string.Join(Environment.NewLine, _invalidTemplates);
                    Logger.WriteError($"Invalid Templates:{Environment.NewLine}{templateList}");
                }

                _environmentService.Exit(1);
            }
        }
    }
}
