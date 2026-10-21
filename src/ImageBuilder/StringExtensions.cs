// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder;

public static class StringExtensions
{
    /// <summary>
    /// Compare two strings and return the index of the first difference.  Returns -1 if the strings are equal.
    /// </summary>
    public static int DiffersAtIndex(this string s1, string s2)
    {
        int index = 0;
        int min = Math.Min(s1.Length, s2.Length);
        while (index < min && s1[index] == s2[index])
        {
            index++;
        }

        return (index == min && s1.Length == s2.Length) ? -1 : index;
    }

    public static string FirstCharToUpper(this string source) => char.ToUpper(source[0]) + source[1..];

    public static string TrimEndString(this string source, string trimString)
    {
        if (string.IsNullOrEmpty(trimString))
        {
            return source;
        }

        while (source.EndsWith(trimString))
        {
            source = source[..^trimString.Length];
        }

        return source;
    }

    public static string GetLineEndingFormat(this string value) => value.Contains("\r\n") ? "\r\n" : "\n";

    public static string TrimStartString(this string source, string trimString)
    {
        if (string.IsNullOrEmpty(trimString))
        {
            return source;
        }

        while (source.StartsWith(trimString))
        {
            source = source[trimString.Length..];
        }

        return source;
    }

    public static string ToCamelCase(this string source) => source[..1].ToLowerInvariant() + source[1..];

    public static string NormalizeLineEndings(this string value, string targetFormat)
    {
        string targetLineEnding = targetFormat.GetLineEndingFormat();
        string valueLineEnding = value.GetLineEndingFormat();
        if (valueLineEnding != targetLineEnding)
        {
            value = value.Replace(valueLineEnding, targetLineEnding);
        }

        // Make sure the value ends with a blank line if the target ends with a blank line
        if (targetFormat.Last() == '\n' && value.Last() != '\n')
        {
            value += targetLineEnding;
        }

        return value;
    }

    public static (string Key, string Value) ParseKeyValuePair(this string value, char delimiter)
    {
        int firstEqualIndex = value.IndexOf(delimiter);
        return (value[..firstEqualIndex], value[(firstEqualIndex + 1)..]);
    }
}
