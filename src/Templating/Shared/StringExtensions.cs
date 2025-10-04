// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.DockerTools.Templating.Shared;

internal static class StringExtensions
{
    public static string FirstCharToUpper(this string source) => char.ToUpper(source[0]) + source.Substring(1);

    [return: NotNullIfNotNull(nameof(source))]
    public static string? TrimStartString(this string? source, string trim) => source switch
    {
        string s when s.StartsWith(trim) => s.Substring(trim.Length).TrimStartString(trim),
        _ => source,
    };

    [return: NotNullIfNotNull(nameof(source))]
    public static string? TrimEndString(this string? source, string trim) => source switch
    {
        string s when s.EndsWith(trim) => s.Substring(0, s.Length - trim.Length).TrimEndString(trim),
        _ => source,
    };
}

internal static class EnumerableExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        public IEnumerable<(T Item, int Index)> WithIndex() =>
            source.Select((item, index) => (item, index));
    }
}
