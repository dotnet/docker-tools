// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

internal sealed partial class VariableHelper
{
    private const char BuiltInDelimiter = ':';
    private const string RepoVariableTypeId = "Repo";
    private const string VariableGroupName = "variable";
    private const string TagVariablePattern = $"\\$\\((?<{VariableGroupName}>[\\w:\\-.| ]+)\\)";

    private Manifest Manifest { get; }

    public IDictionary<string, string?> ResolvedVariables { get; } = new Dictionary<string, string?>();

    public VariableHelper(Manifest manifest, IEnumerable<RepoInfo> repos)
    {
        Manifest = manifest;

        if (Manifest.Variables is not null)
        {
            foreach (var (key, value) in Manifest.Variables)
            {
                var variableValue = SubstituteValues(value);
                ResolvedVariables.Add(key, variableValue);
            }
        }
    }

    private string SubstituteValues(string expression, Func<string, string, string>? getContextBasedSystemValue = null)
    {
        foreach (Match match in TagVariableRegex.Matches(expression))
        {
            string variableName = match.Groups[VariableGroupName].Value;
            string? variableValue = variableName.Contains(BuiltInDelimiter)
                ? GetBuiltInValue(variableName, getContextBasedSystemValue)
                : GetResolvedValue(variableName);

            if (variableValue is null)
            {
                throw new InvalidOperationException($"A value was not found for the variable '{match.Value}'");
            }

            expression = expression.Replace(match.Value, variableValue);
        }
        return expression;
    }

    private string? GetBuiltInValue(string variableName)
    {
        string[] parts = variableName.Split(BuiltInDelimiter, 2);
        string variableType = parts[0];
        string remainder = parts[1];

        if (string.Equals(variableType, RepoVariableTypeId, StringComparison.Ordinal))
        {
            // Optional fallback: match by name if Ids are sparse
            var byName = Repos.FirstOrDefault(r => r.Model.Name == remainder);
            return byName?.QualifiedName;
        }

        return getContextBasedSystemValue?.Invoke(variableType, remainder);
    }

    private string? GetResolvedValue(string variableName)
    {
        ResolvedVariables.TryGetValue(variableName, out string? variableValue);
        return variableValue;
    }

    [GeneratedRegex(TagVariablePattern)]
    private partial Regex TagVariableRegex { get; }
}
