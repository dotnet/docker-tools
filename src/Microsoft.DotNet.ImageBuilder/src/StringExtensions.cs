// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class StringExtensions
    {
        public static string FirstCharToUpper(this string source) => char.ToUpper(source[0]) + source.Substring(1);

        public static string TrimEnd(this string source, string trimString)
        {
            while (source.EndsWith(trimString))
            {
                source = source.Substring(0, source.Length - trimString.Length);
            }

            return source;
        }

        public static string TrimStart(this string source, string trimString)
        {
            while (source.StartsWith(trimString))
            {
                source = source.Substring(trimString.Length);
            }

            return source;
        }

        public static string ToCamelCase(this string source)
        {
            return source.Substring(0, 1).ToLowerInvariant() + source.Substring(1);
        }

        public static string NormalizeLineEndings(this string value, string targetFormat)
        {
            string targetLineEnding = targetFormat.Contains("\r\n") ? "\r\n" : "\n";
            string valueLineEnding = value.Contains("\r\n") ? "\r\n" : "\n";
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
    }
}
