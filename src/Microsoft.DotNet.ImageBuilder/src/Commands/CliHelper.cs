// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public static class CliHelper
    {
        public static Option<T> CreateOption<T>(string alias, string propertyName, string description, T defaultValue = default) =>
            new Option<T>(FormatAlias(alias), () => defaultValue!, description)
            {
                Name = propertyName
            };

        public static Option<T> CreateOption<T>(string alias, string propertyName, string description, ParseArgument<T> parseArg) =>
            new Option<T>(FormatAlias(alias), parseArg, description: description)
            {
                Name = propertyName
            };

        public static Option<T> CreateOption<T>(string alias, string propertyName, string description, Func<string, T> convert,
            T defaultValue = default) =>
            new Option<T>(FormatAlias(alias), description: description,
                parseArgument: resultArg => convert(GetTokenValue(resultArg)))
            {
                Argument = new Argument<T>(() => defaultValue!),
                Name = propertyName
            };

        public static Option<T[]> CreateMultiOption<T>(string alias, string propertyName, string description) =>
            new Option<T[]>(FormatAlias(alias), () => Array.Empty<T>(), description)
            {
                Name = propertyName,
                AllowMultipleArgumentsPerToken = false
            };

        public static Option<Dictionary<string, string>> CreateDictionaryOption(string alias, string propertyName, string description) =>
            new Option<Dictionary<string, string>>(FormatAlias(alias), description: description,
                parseArgument: argResult =>
                {
                    return argResult.Tokens
                        .ToList()
                        .Select(token => token.Value.Split(new char[] { '=' }, 2))
                        .ToDictionary(split => split[0], split => split[1]);
                })
            {
                Name = nameof(ManifestOptions.Variables),
                AllowMultipleArgumentsPerToken = false
            };

        public static string GetTokenValue(this SymbolResult symbolResult) => symbolResult.Tokens.First().Value;

        public static string FormatAlias(string alias) => $"--{alias}";
    }
}
#nullable disable
