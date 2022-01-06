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

        protected GenerateArtifactsCommand(IEnvironmentService environmentService) : base()
        {
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        protected async Task GenerateArtifactsAsync<TContext>(
            IEnumerable<TContext> contexts,
            Func<TContext, string> getTemplatePath,
            Func<TContext, string> getArtifactPath,
            Func<TContext, string, IReadOnlyDictionary<Value, Value>> getSymbols,
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

                await GenerateArtifactAsync(templatePath, artifactPath, context, getSymbols, artifactName, postProcess);
            }
        }

        private async Task GenerateArtifactAsync<TContext>(
            string templatePath,
            string artifactPath,
            TContext context,
            Func<TContext, string, IReadOnlyDictionary<Value, Value>> getSymbols,
            string artifactName,
            Func<string, TContext, string> postProcess)
        {
            Logger.WriteSubheading($"Generating '{artifactPath}' from '{templatePath}'");

            string generatedArtifact = await RenderTemplateAsync(templatePath, context, getSymbols, Value.EmptyMap, null, trimTemplate: false);

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
            Func<TContext, string, IReadOnlyDictionary<Value, Value>> getSymbols)
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
                                getSymbols,
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
            Func<TContext, string, IReadOnlyDictionary<Value, Value>> getSymbols,
            Value templateArgs,
            string indent,
            bool trimTemplate)
        {
            string artifact = null;

            string template = await File.ReadAllTextAsync(templatePath);

            if (trimTemplate)
            {
                template = template.Trim();
            }

            if (!string.IsNullOrEmpty(indent))
            {
                // Indents all the lines except the first one
                template = template.Replace("\n", $"\n{indent}");
            }

            if (Options.IsVerbose)
            {
                Logger.WriteMessage($"Template:{Environment.NewLine}{template}");
            }

            try
            {
                IDocument document = Document.CreateDefault(template, _config).DocumentOrThrow;
                IReadOnlyDictionary<Value, Value> symbols = new Dictionary<Value, Value>(getSymbols(context, templatePath))
                {
                    { "ARGS", new Dictionary<Value, Value>(templateArgs.Fields) }
                };
                
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
