// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder
{
    public class ReadmeHelper
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

            Regex regex = new Regex($"^{TagsSectionHeader}\\s*(^(?!# ).*\\s)*", RegexOptions.Multiline);
            return regex.Replace(readme, tagsListing);
        }
    }
}
