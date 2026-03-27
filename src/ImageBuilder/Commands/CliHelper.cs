// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public static class CliHelper
    {
        public static string GetTokenValue(this SymbolResult symbolResult) => symbolResult.Tokens.First().Value;

        public static string FormatAlias(string alias) => $"--{alias}";

        /// <summary>
        /// Creates a dictionary option that parses key=value pairs from the command line.
        /// </summary>
        public static Option<Dictionary<string, TValue>> CreateDictionaryOption<TValue>(
            string alias,
            string description,
            Func<string, TValue> getValue) =>
            new Option<Dictionary<string, TValue>>(FormatAlias(alias))
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
            string alias,
            string description) =>
            CreateDictionaryOption(alias, description, val => val);
    }
}
