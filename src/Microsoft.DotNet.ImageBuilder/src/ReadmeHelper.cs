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

        public static string UpdateTagsListing(string readme, string tagsListing)
        {
            // Normalize the line endings to match the readme.
            tagsListing = tagsListing.NormalizeLineEndings(readme);

            string targetLineEnding = readme.GetLineEndingFormat();
            tagsListing = $"{TagsSectionHeader}{targetLineEnding}{targetLineEnding}{tagsListing}{targetLineEnding}";

            // Regex to find the entire tags listing section including the header.
            Regex regex = new Regex($"^{TagsSectionHeader}\\s*(^(?!# ).*\\s)*", RegexOptions.Multiline);
            return regex.Replace(readme, tagsListing);
        }
    }
}
