// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

            // We need to account for two readme scenarios:
            //   1. Readmes hosted in the repo
            //   2. Readmes published to mcrdocs
            // The first scenario is simple because it just places the generated tag listing after the tag listing header.
            // The second scenario is the tricky one because we start with a fully populated readme and we need to strip
            // out the tag listing and include marker text for MCR's tooling to insert its own tag listing. In order to
            // determine which portion of text needs to be stripped out we rely on an "End of generated tags" marker comment
            // in the readme. So we strip out everything starting after the tag listing header and up to the marker comment.
            // Both scenarios can be solved with this single algorithm.

            int fullTagListingHeaderIndex = readme.IndexOf(TagsSectionHeader);
            if (fullTagListingHeaderIndex >= 0)
            {
                int endOfFullTagListingHeaderIndex = readme.IndexOf(TagsSectionHeader) + TagsSectionHeader.Length;
                int endOfGeneratedTagsIndex = readme.IndexOf(EndOfGeneratedTagsMarker) + EndOfGeneratedTagsMarker.Length;

                readme =
                    readme.Substring(0, endOfFullTagListingHeaderIndex) +
                    targetLineEnding +
                    targetLineEnding +
                    tagsListing +
                    readme.Substring(endOfGeneratedTagsIndex);
            }

            return readme;
        }
    }
}
