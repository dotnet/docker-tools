#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder;

public static class SystemCommandLineExtensions
{
    public static bool Has(this CommandResult commandResult, IOption option) =>
        commandResult.FindResultFor(option)?.Tokens?.Count > 0;
}
