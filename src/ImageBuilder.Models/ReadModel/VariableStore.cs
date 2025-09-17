// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

internal sealed partial class VariableStore : IVariableStore
{
    private const string VariableGroupName = "variable";

    private readonly Dictionary<string, string> _resolvedVariables;

    /// <summary>
    /// Creates a new <see cref="VariableStore"/>.
    /// </summary>
    /// <param name="variables">
    /// The order of variable definitions determines which order they are
    /// resolved in. Variable references must come after their definition.
    /// </param>
    public VariableStore(IDictionary<string, string> variables)
    {
        _resolvedVariables = new Dictionary<string, string>();
        foreach (var (variable, unresolvedValue) in variables)
        {
            string resolvedValue = ResolveInnerVariables(unresolvedValue);
            _resolvedVariables.Add(variable, resolvedValue);
        }
    }

    /// <summary>
    /// Evaluates an expression and replaces any variables inside with their
    /// fully-resolved values.
    /// </summary>
    /// <param name="expression">
    /// Variable references inside this expression will be replaced.
    /// </param>
    public string ResolveInnerVariables(string expression)
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
