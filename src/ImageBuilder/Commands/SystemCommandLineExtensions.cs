// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public static class SystemCommandLineExtensions
{
    /// <summary>
    /// Adds the arguments, options, and validators defined by the given
    /// <see cref="Options"/> instance to the command.
    /// </summary>
    public static void AddOptions(this Command command, Options options)
    {
        foreach (Argument argument in options.GetCliArguments())
            command.Add(argument);

        foreach (Option option in options.GetCliOptions())
            command.Add(option);

        foreach (Action<CommandResult> validator in options.GetValidators())
            command.Validators.Add(validator);
    }

    /// <summary>
    /// Gets a value indicating whether the specified option was provided on the command line.
    /// </summary>
    public static bool Has(this CommandResult commandResult, Option option) =>
        commandResult.GetResult(option)?.Tokens?.Count > 0;
}
