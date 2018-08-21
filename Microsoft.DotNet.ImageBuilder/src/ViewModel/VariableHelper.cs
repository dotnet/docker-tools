// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class VariableHelper
    {
        private const char BuiltInDelimiter = ':';
        public const string DockerfileGitCommitShaVariableName = "DockerfileGitCommitSha";
        public const string SourceUrlVariableName = "SourceUrl";
        public const string SystemVariableTypeId = "System";
        public const string TagDocTypeId = "TagDoc";
        public const string TagDocListTypeId = "TagDocList";
        private const string TagVariableTypeId = "TagRef";
        private const string TimeStampVariableName = "TimeStamp";
        private const string VariableGroupName = "variable";

        private static string TagVariablePattern = $"\\$\\((?<{VariableGroupName}>[\\w:\\-.|]+)\\)";
        private static string TimeStamp { get; } = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        private Func<string, TagInfo> GetTagById { get; set; }
        private Manifest Manifest { get; set; }
        private IOptionsInfo Options { get; set; }

        public VariableHelper(
            Manifest manifest,
            IOptionsInfo options,
            Func<string, TagInfo> getTagById)
        {
            GetTagById = getTagById;
            Manifest = manifest;
            Options = options;
        }

        public string SubstituteValues(string expression, Func<string, string, string> getContextBasedSystemValue = null)
        {
            foreach (Match match in Regex.Matches(expression, TagVariablePattern))
            {
                string variableValue;
                string variableName = match.Groups[VariableGroupName].Value;

                if (variableName.Contains(BuiltInDelimiter))
                {
                    variableValue = GetBuiltInValue(variableName, getContextBasedSystemValue);
                }
                else
                {
                    variableValue = GetUserValue(variableName);
                }

                if (variableValue == null)
                {
                    throw new InvalidOperationException($"A value was not found for the variable '{match.Value}'");
                }

                expression = expression.Replace(match.Value, variableValue);
            }

            return expression;
        }

        private string GetBuiltInValue(string variableName, Func<string, string, string> getContextBasedSystemValue)
        {
            string variableValue = null;

            string[] variableNameParts = variableName.Split(BuiltInDelimiter, 2);
            string variableType = variableNameParts[0];
            variableName = variableNameParts[1];

            if (string.Equals(variableType, SystemVariableTypeId, StringComparison.Ordinal))
            {
                if (string.Equals(variableName, TimeStampVariableName, StringComparison.Ordinal))
                {
                    variableValue = TimeStamp;
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
            else if (string.Equals(variableType, TagVariableTypeId, StringComparison.Ordinal))
            {
                variableValue = GetTagById(variableName)?.Name;
            }
            else if (getContextBasedSystemValue != null)
            {
                variableValue = getContextBasedSystemValue(variableType, variableName);
            }

            return variableValue;
        }

        private string GetUserValue(string variableName)
        {
            if (!Options.Variables.TryGetValue(variableName, out string variableValue))
            {
                Manifest.Variables?.TryGetValue(variableName, out variableValue);
            }

            return variableValue;
        }
    }
}
