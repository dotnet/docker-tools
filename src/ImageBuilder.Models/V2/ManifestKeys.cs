// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public readonly record struct ImageKey(string Repo, string Image)
{
    public override string ToString() => $"{Repo}{KeyPatterns.Separator}{Image}";
}

public readonly record struct PlatformKey(string Repo, string Image, string Platform)
{
    public override string ToString() => $"{Repo}{KeyPatterns.Separator}{Image}{KeyPatterns.Separator}{Platform}";
}

internal static partial class KeyPatterns
{
    public const string Separator = "__";

    public const string Description = "letters and digits separated by single '.', '_' or '-'";

    private const string PartPattern = "[a-zA-Z0-9]+(?:[._-][a-zA-Z0-9]+)*";

    [GeneratedRegex($@"^{PartPattern}\z")]
    public static partial Regex Part { get; }
}
