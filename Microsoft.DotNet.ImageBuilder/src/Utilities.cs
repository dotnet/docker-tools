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
            Func<string, string> getVariableValue = null)
        {
            foreach (Match match in Regex.Matches(expression, TagVariablePattern))
            {
                string variableName = match.Groups[1].Value;
                string variableValue = null;
                if (variables == null || !variables.TryGetValue(variableName, out variableValue))
                {
                    if (getVariableValue != null)
                    {
                        variableValue = getVariableValue(variableName);
                    }
                    if (variableValue == null && match.Value == "$(timeStamp)")
                    {
                        variableValue = TimeStamp;
                    }
                }
                if (variableValue == null)
                {
                    throw new InvalidDataException(
                            $"Unable to determine the value for the given variable. Variable: {match.Value}");
                }
                expression = expression.Replace(match.Value, variableValue);
            }

            return expression;
        }

        public static Func<string, string> GetSha(string filePath, bool isDryRun = false)
        {
            return delegate (string variableName)
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Unable to find the file.", filePath);
                }
                if (!string.Equals("dockerfileSha", variableName))
                {
                    return null;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo("git", $"log -1 --format=format:%h {filePath}");
                startInfo.RedirectStandardOutput = true;
                Process gitLogProcess = ExecuteHelper.Execute(
                    startInfo, isDryRun, $"Unable to retrieve the commit timestamp for {filePath}");
                return isDryRun ? "" : gitLogProcess.StandardOutput.ReadToEnd().Trim();
            };
        }

        public static void WriteHeading(string heading)
        {
            Console.WriteLine();
            Console.WriteLine(heading);
            Console.WriteLine(new string('-', heading.Length));
        }
    }
}
