// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder.Tests.CommandLine;

/// <summary>
/// Provides shared helpers for exercising options types through the real command registration and
/// binding path used by the CLI.
/// </summary>
internal static class OptionsBindingTestHelper
{
    /// <summary>
    /// Parses the supplied command line args using the real command registration path for the given
    /// <see cref="Options"/> instance.
    /// </summary>
    internal static ParseResult Parse(Options options, string[] args)
    {
        Command command = new("test", "test");
        command.AddOptions(options);
        return command.Parse(GetParseArgs(options, args));
    }

    /// <summary>
    /// Creates a test options instance, parses the supplied args through the normal command flow,
    /// and binds the parsed values back onto that same instance.
    /// </summary>
    internal static TOptions ParseAndBind<TOptions>(string[] args)
        where TOptions : Options, new()
    {
        TOptions options = new();
        ParseResult parseResult = Parse(options, args);
        options.Bind(parseResult);
        return options;
    }

    private static string[] GetParseArgs(Options options, string[] args)
    {
        if (options is not IFilterableOptions and not IPlatformFilterableOptions)
        {
            return args;
        }

        return
        [
            ..args,
            ..GetOptionArgs(args, "--architecture", "amd64"),
            ..GetOptionArgs(args, "--os-type", "linux"),
        ];
    }

    private static string[] GetOptionArgs(string[] args, string optionName, string optionValue) =>
        HasOption(args, optionName) ? [] : [optionName, optionValue];

    private static bool HasOption(string[] args, string optionName) =>
        args.Any(arg => arg == optionName || arg.StartsWith($"{optionName}=", StringComparison.Ordinal));
}
