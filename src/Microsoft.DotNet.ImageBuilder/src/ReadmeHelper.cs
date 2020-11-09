// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Sprache;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ReadmeHelper
    {
        private const string TagsSectionHeader = "# Full Tag Listing";
        private const string EndOfGeneratedTagsMarker = "<!--End of generated tags-->";

        public static string UpdateTagsListing(string readme, string tagsListing)
        {
            // Normalize the line endings to match the readme.
            tagsListing = tagsListing.NormalizeLineEndings(readme);

            string targetLineEnding = readme.GetLineEndingFormat();

            Parser<string> parser =
                from leadingContent in Parse.AnyChar.Until(Parse.String(TagsSectionHeader + targetLineEnding)).Text()
                from tagsContent in Parse.AnyChar.Until(Parse.String(EndOfGeneratedTagsMarker + targetLineEnding)).Text()
                from trailingContent in Parse.AnyChar.Many().Text()
                select string.Concat(
                    leadingContent,
                    TagsSectionHeader,
                    targetLineEnding,
                    targetLineEnding,
                    tagsListing,
                    EndOfGeneratedTagsMarker,
                    targetLineEnding,
                    trailingContent);

            return parser.Parse(readme);
        }
    }
}
