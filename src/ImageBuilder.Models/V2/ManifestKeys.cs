// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public readonly record struct ImageKey(string Repo, string Image)
{
    public override string ToString() => $"{Repo}__{Image}";
}

public readonly record struct PlatformKey(string Repo, string Image, string Platform)
{
    public override string ToString() => $"{Repo}__{Image}__{Platform}";
}

internal static partial class KeyPatterns
{
    public const string Description = "lowercase letters and digits separated by single '.', '_' or '-'";

    private const string Part = "[a-z0-9]+(?:[._-][a-z0-9]+)*";

    [GeneratedRegex($"^{Part}__{Part}$")]
    public static partial Regex ImageKey { get; }

    [GeneratedRegex($"^{Part}__{Part}__{Part}$")]
    public static partial Regex PlatformKey { get; }
}
