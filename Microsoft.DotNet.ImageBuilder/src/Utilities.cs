// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class Utilities
    {
        private static string TimeStamp { get; } = DateTime.UtcNow.ToString("yyyymmddhhmmss");
        private const string TagVariablePattern = "\\$\\((?<variable>[\\w]+)\\)";

        public static string SubstituteVariables(
            IDictionary<string, string> variables,
            string expression,
            Func<string, string> getVariableSubstitute = null)
        {
            foreach (Match match in Regex.Matches(expression, TagVariablePattern))
            {
                string variableName = match.Groups["variable"].Value;
                string variableValue = null;
                if (variables == null || !variables.TryGetValue(variableName, out variableValue))
                {
                    if (getVariableSubstitute != null)
                    {
                        variableValue = getVariableSubstitute(variableName);
                    }
                    if (variableValue == null && match.Value == "$(TimeStamp)")
                    {
                        variableValue = TimeStamp;
                    }
                    if (variableValue == null)
                    {
                        throw new InvalidDataException($"A value was not found for the variable '{match.Value}'");
                    }
                }
                expression = expression.Replace(match.Value, variableValue);
            }

            return expression;
        }

        public static string GetAbbreviatedCommitSha(string filePath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git", $"log -1 --format=format:%h {filePath}");
            startInfo.RedirectStandardOutput = true;
            Process gitLogProcess = ExecuteHelper.Execute(
                startInfo, false, $"Unable to retrieve the commit for {filePath}");
            return gitLogProcess.StandardOutput.ReadToEnd().Trim();
        }

        public static void WriteHeading(string heading)
        {
            Console.WriteLine();
            Console.WriteLine(heading);
            Console.WriteLine(new string('-', heading.Length));
        }
    }
}
