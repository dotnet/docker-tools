// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ReadmeHelper
    {
        private const string TagsSectionHeader = "# Full Tag Listing";

        private static string NormalizeLineEndings(string value, string targetFormat)
        {
            string targetLineEnding = targetFormat.Contains("\r\n") ? "\r\n" : "\n";
            string valueLineEnding = value.Contains("\r\n") ? "\r\n" : "\n";
            if (valueLineEnding != targetLineEnding)
            {
                value = value.Replace(valueLineEnding, targetLineEnding);
            }

            return value;
        }

        public static string UpdateTagsListing(string readme, string tagsListing)
        {
            tagsListing = $"{TagsSectionHeader}{Environment.NewLine}{Environment.NewLine}{tagsListing}{Environment.NewLine}{Environment.NewLine}";

            // Normalize the line endings to match the readme.
            tagsListing = NormalizeLineEndings(tagsListing, readme);

            // Regex to find the entire tags listing section including the header.
            Regex regex = new Regex($"^{TagsSectionHeader}\\s*(^(?!# ).*\\s)*", RegexOptions.Multiline);
            return regex.Replace(readme, tagsListing);
        }
    }
}
