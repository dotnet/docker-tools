// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class VariableHelper
    {
        private const char BuiltInDelimiter = ':';
        public const string DockerfileGitCommitShaVariableName = "DockerfileGitCommitSha";
        public const string McrTagsYmlRepoTypeId = "McrTagsYmlRepo";
        public const string McrTagsYmlTagGroupTypeId = "McrTagsYmlTagGroup";
        public const string RepoVariableTypeId = "Repo";
        public const string SystemVariableTypeId = "System";
        private const string TimeStampVariableName = "TimeStamp";
        private const string VariableGroupName = "variable";

        private static readonly string s_tagVariablePattern = $"\\$\\((?<{VariableGroupName}>[\\w:\\-.|]+)\\)";
        private static readonly string s_timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        private Func<string, RepoInfo> GetRepoById { get; set; }
        private Manifest Manifest { get; set; }
        private IManifestOptionsInfo Options { get; set; }

        public IDictionary<string, string?> ResolvedVariables { get; } = new Dictionary<string, string?>();

        public VariableHelper(Manifest manifest, IManifestOptionsInfo options, Func<string, RepoInfo> getRepoById)
        {
            GetRepoById = getRepoById;
            Manifest = manifest;
            Options = options;

            if (Manifest.Variables is not null)
            {
                foreach (KeyValuePair<string, string?> kvp in Manifest.Variables)
                {
                    string? variableValue;
                    if (Options.Variables is not null && Options.Variables.TryGetValue(kvp.Key, out string? overridenValue))
                    {
                        variableValue = overridenValue;
                    }
                    else
                    {
                        variableValue = kvp.Value;
                    }

                    variableValue = SubstituteValues(variableValue);
                    ResolvedVariables.Add(kvp.Key, variableValue);
                }
            }
            else if (Options.Variables is not null)
            {
                ResolvedVariables = new Dictionary<string, string?>(Options.Variables);
            }
        }

        public string? SubstituteValues(string? expression, Func<string, string, string>? getContextBasedSystemValue = null)
        {
            if (expression == null)
            {
                return null;
            }

            foreach (Match match in Regex.Matches(expression, s_tagVariablePattern))
            {
                string? variableValue;
                string variableName = match.Groups[VariableGroupName].Value;

                if (variableName.Contains(BuiltInDelimiter))
                {
                    variableValue = GetBuiltInValue(variableName, getContextBasedSystemValue);
                }
                else
                {
                    variableValue = GetUserValue(variableName);

                    if (variableValue is null && Manifest.Variables.ContainsKey(variableName))
                    {
                        throw new NotSupportedException(
                            $"Unable to resolve value for variable '{variableName}' because it references a variable whose value hasn't been resolved yet. Dependencies between variables need to be ordered according to their dependency.");
                    }
                }

                if (variableValue == null)
                {
                    throw new InvalidOperationException($"A value was not found for the variable '{match.Value}'");
                }

                expression = expression.Replace(match.Value, variableValue);
            }

            return expression;
        }

        private string? GetBuiltInValue(string variableName, Func<string, string, string>? getContextBasedSystemValue)
        {
            string? variableValue = null;

            string[] variableNameParts = variableName.Split(BuiltInDelimiter, 2);
            string variableType = variableNameParts[0];
            variableName = variableNameParts[1];

            if (string.Equals(variableType, SystemVariableTypeId, StringComparison.Ordinal))
            {
                if (string.Equals(variableName, TimeStampVariableName, StringComparison.Ordinal))
                {
                    variableValue = s_timeStamp;
                }
                else if (getContextBasedSystemValue != null)
                {
                    variableValue = getContextBasedSystemValue(variableType, variableName);
                }
                else
                {
                    variableValue = Options.GetOption(variableName);
                }
            }
            else if (string.Equals(variableType, RepoVariableTypeId, StringComparison.Ordinal))
            {
                variableValue = GetRepoById(variableName)?.QualifiedName;
            }
            else if (getContextBasedSystemValue != null)
            {
                variableValue = getContextBasedSystemValue(variableType, variableName);
            }

            return variableValue;
        }

        private string? GetUserValue(string variableName)
        {
            string? variableValue = null;
            if (!Options.Variables?.TryGetValue(variableName, out variableValue) == true)
            {
                ResolvedVariables?.TryGetValue(variableName, out variableValue);
            }

            return variableValue;
        }
    }
}
#nullable disable
