// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder
{
    public static class StringExtensions
    {
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
    }
}
