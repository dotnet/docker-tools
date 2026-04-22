// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public static class CliHelper
{
    public static string GetTokenValue(this SymbolResult symbolResult) => symbolResult.Tokens.First().Value;

    /// <summary>
    /// Creates a dictionary option that parses key=value pairs from the command line.
    /// </summary>
    public static Option<Dictionary<string, TValue>> CreateDictionaryOption<TValue>(
        string optionName,
        string description,
        Func<string, TValue> getValue) =>
        new(optionName)
        {
            Description = description,
            AllowMultipleArgumentsPerToken = false,
            CustomParser = argResult =>
                argResult.Tokens
                    .ToList()
                    .Select(token => token.Value.ParseKeyValuePair('='))
                    .ToDictionary(kvp => kvp.Key, kvp => getValue(kvp.Value))
        };

    /// <summary>
    /// Creates a dictionary option that parses key=value pairs as strings.
    /// </summary>
    public static Option<Dictionary<string, string>> CreateDictionaryOption(
        string optionName,
        string description) =>
        CreateDictionaryOption(optionName, description, val => val);
}
