// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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

            Parser<string> headerParser =
                from headerPrefix in Parse.String(targetLineEnding + "# ").Text()
                from label in Parse.AnyChar.Until(Parse.String(targetLineEnding)).Text()
                select string.Concat(headerPrefix, label, targetLineEnding);

            Parser<IEnumerable<char>> endOfGeneratedTagsParser =
                Parse.String(EndOfGeneratedTagsMarker + targetLineEnding).XOr(headerParser);

            Parser<string> parser =
                from leadingContent in Parse.AnyChar.Until(Parse.String(TagsSectionHeader + targetLineEnding)).Text()
                from tagsContent in Parse.AnyChar.Except(endOfGeneratedTagsParser).Many().Text()
                from endOfGeneratedTags in endOfGeneratedTagsParser.Text()
                from trailingContent in Parse.AnyChar.Many().Text()
                select string.Concat(
                    leadingContent,
                    TagsSectionHeader,
                    targetLineEnding,
                    targetLineEnding,
                    tagsListing,
                    endOfGeneratedTags,
                    trailingContent);

            return parser.Parse(readme);
        }
    }
}
