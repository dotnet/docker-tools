// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;


#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public static class CliHelper
    {
        public static Option<T> CreateOption<T>(string alias, string propertyName, string description, T defaultValue = default!) =>
            CreateOption(alias, propertyName, description, () => defaultValue);

        public static Option<T> CreateOption<T>(string alias, string propertyName, string description, Func<T> defaultValue) =>
            new Option<T>(FormatAlias(alias), defaultValue!, description)
            {
                Name = propertyName
            };

        public static Option<T> CreateOption<T>(string alias, string propertyName, string description, ParseArgument<T> parseArg) =>
            new Option<T>(FormatAlias(alias), parseArg, description: description)
            {
                Name = propertyName
            };

        public static Option<T> CreateOption<T>(string alias, string propertyName, string description, Func<string, T> convert,
            T defaultValue = default!) =>
            new Option<T>(FormatAlias(alias), description: description,
                parseArgument: resultArg => convert(resultArg.GetTokenValue()))
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
            CreateDictionaryOption(alias, propertyName, description, val => val);

        public static Option<Dictionary<string, TValue>> CreateDictionaryOption<TValue>(string alias, string propertyName, string description,
            Func<string, TValue> getValue) =>
            new Option<Dictionary<string, TValue>>(FormatAlias(alias), description: description,
                parseArgument: argResult =>
                {
                    return argResult.Tokens
                        .ToList()
                        .Select(token => token.Value.ParseKeyValuePair('='))
                        .ToDictionary(kvp => kvp.Key, kvp => getValue(kvp.Value));
                })
            {
                Name = propertyName,
                AllowMultipleArgumentsPerToken = false
            };

        public static string GetTokenValue(this SymbolResult symbolResult) => symbolResult.Tokens.First().Value;

        public static string FormatAlias(string alias) => $"--{alias}";
    }
}
#nullable disable
