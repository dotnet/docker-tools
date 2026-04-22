// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;
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
        return command.Parse(args);
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
}
