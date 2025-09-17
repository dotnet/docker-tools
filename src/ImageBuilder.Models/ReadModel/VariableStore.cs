// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

internal sealed partial class VariableStore
{
    private const string VariableGroupName = "variable";

    private ImmutableDictionary<string, string> _resolvedVariables { get; }

    public VariableStore(IDictionary<string, string> variables)
    {
        var resolvedVariables = new Dictionary<string, string>();
        foreach (var (variable, unresolvedValue) in variables)
        {
            string resolvedValue = ResolveInnerVariables(unresolvedValue);
            resolvedVariables.Add(variable, resolvedValue);
        }

        _resolvedVariables = resolvedVariables.ToImmutableDictionary();
    }

    public string? Get(string variableName) => GetResolvedValue(variableName);

    /// <summary>
    /// Evaluates an expression and replaces any variables inside with their
    /// fully-resolved values.
    /// </summary>
    /// <param name="expression">
    /// Variable references inside this expression will be replaced.
    /// </param>
    private string ResolveInnerVariables(string expression)
    {
        var subVariableMatches = TagVariableRegex.Matches(expression);
        foreach (Match match in subVariableMatches)
        {
            string variableName = match.Groups[VariableGroupName].Value;
            string? variableValue = GetResolvedValue(variableName)
                ?? throw new InvalidOperationException($"A value was not found for the variable '{match.Value}'");
            expression = expression.Replace(match.Value, variableValue);
        }

        return expression;
    }

    /// <summary>
    /// Get the resolved value for a variable name. Returns null if the
    /// variable is not found.
    /// </summary>
    /// <returns>
    /// The variable value, or null if the variable is not found
    /// </returns>
    private string? GetResolvedValue(string variableName)
    {
        _resolvedVariables.TryGetValue(variableName, out string? variableValue);
        return variableValue;
    }

    [GeneratedRegex($"\\$\\((?<{VariableGroupName}>[\\w:\\-.| ]+)\\)")]
    private static partial Regex TagVariableRegex { get; }
}
