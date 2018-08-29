// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder
{
    public static class StringExtensions
    {
        public static string TrimEnd(this string source, string value)
        {
            return source.EndsWith(value) ? source.Remove(source.LastIndexOf(value)) : source;
        }
    }
}
