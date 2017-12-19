// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class Utilities
    {
        private static string TimeStamp { get; } = DateTime.UtcNow.ToString("yyyymmddhhmmss");
        private const string VariableGroupName = "variable";
        private static string TagVariablePattern = $"\\$\\((?<{VariableGroupName}>[\\w]+)\\)";

        public static string SubstituteVariables(
            IDictionary<string, string> userVariables,
            string expression,
            Func<string, string> getSystemValue = null)
        {
            foreach (Match match in Regex.Matches(expression, TagVariablePattern))
            {
                string variableName = match.Groups[VariableGroupName].Value;
                string variableValue = null;
                if (userVariables == null || !userVariables.TryGetValue(variableName, out variableValue))
                {
                    if (getSystemValue != null)
                    {
                        variableValue = getSystemValue(variableName);
                    }
                    if (variableValue == null && variableName == "TimeStamp")
                    {
                        variableValue = TimeStamp;
                    }
                    if (variableValue == null)
                    {
                        throw new InvalidOperationException($"A value was not found for the variable '{match.Value}'");
                    }
                }
                expression = expression.Replace(match.Value, variableValue);
            }

            return expression;
        }

        public static void WriteHeading(string heading)
        {
            Console.WriteLine();
            Console.WriteLine(heading);
            Console.WriteLine(new string('-', heading.Length));
        }
    }
}
