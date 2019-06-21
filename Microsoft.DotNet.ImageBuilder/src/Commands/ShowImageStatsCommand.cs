// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ShowImageStatsCommand : ManifestCommand<ShowImageStatsOptions>
    {
        public ShowImageStatsCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("IMAGE STATISTICS");

            PlatformInfo[] platforms = Manifest.GetFilteredPlatforms().ToArray();
            LogGeneralStats(platforms);
            LogBaseImageStats(platforms);

            return Task.CompletedTask;
        }

        private string FormatBaseImageStats(
            string baseImage,
            object dependentImages,
            object dependentConcreteTags,
            object dependentSharedTags,
            object dependentTags)
            => $"{baseImage,-50}  {dependentImages,17}  {dependentConcreteTags,24}  {dependentSharedTags,21}  {dependentTags,20}";

        private void LogBaseImageStats(PlatformInfo[] platforms)
        {
            IEnumerable<string> externalBaseImages = platforms
                .SelectMany(platform => platform.ExternalFromImages)
                .Distinct()
                .OrderBy(name => name)
                .Select(name => $"{name}");

            Logger.WriteMessage();
            Logger.WriteHeading(
                FormatBaseImageStats(
                    $"External Base Images ({externalBaseImages.Count()})",
                    "Dependent Images",
                    "Dependent Concrete Tags",
                    "Dependent Shared Tags",
                    "Total Dependent Tags"));

            foreach (string baseImage in externalBaseImages)
            {
                PlatformInfo[] dependentPlatforms = platforms
                    .Where(platform => platform.ExternalFromImages.Contains(baseImage))
                    .SelectMany(platform => platform.GetDependencyGraph(platforms))
                    .Distinct()
                    .ToArray();
                int dependentPlatformTagsCount = dependentPlatforms.SelectMany(platform => platform.Tags).Count();
                int dependentSharedTagsCount = Manifest.GetFilteredImages()
                    .Where(image => image.FilteredPlatforms.Intersect(dependentPlatforms).Any())
                    .SelectMany(image => image.SharedTags)
                    .Count();

                Logger.WriteMessage(
                    FormatBaseImageStats(
                        baseImage,
                        dependentPlatforms.Length,
                        dependentPlatformTagsCount,
                        dependentSharedTagsCount,
                        dependentPlatformTagsCount + dependentSharedTagsCount));
            }
        }

        private void LogGeneralStats(PlatformInfo[] platforms)
        {
            TagInfo[] platformTags = Manifest.GetFilteredPlatformTags().ToArray();
            TagInfo[] sharedTags = Manifest.GetFilteredImages().SelectMany(image => image.SharedTags).ToArray();
            TagInfo[] undocumentedPlatformTags = platformTags.Where(tag => tag.Model.IsUndocumented).ToArray();
            TagInfo[] undocumentedSharedTags = sharedTags.Where(tag => tag.Model.IsUndocumented).ToArray();

            Logger.WriteMessage($"Total Unique Images:  {platforms.Length}");
            Logger.WriteMessage($"Total Concrete Tags:  {platformTags.Length}");

            if (undocumentedPlatformTags.Length > 0)
            {
                Logger.WriteMessage($"    Total Undocumented Concrete Tags:  {undocumentedPlatformTags.Length}");
            }

            Logger.WriteMessage($"Total Shared Tags:  {sharedTags.Length}");

            if (undocumentedSharedTags.Length > 0)
            {
                Logger.WriteMessage($"    Total Undocumented Shared Tags:  {undocumentedSharedTags.Length}");
            }

            Logger.WriteMessage($"Total Tags:  {platformTags.Length + sharedTags.Length}");

            if (undocumentedPlatformTags.Length > 0 && undocumentedSharedTags.Length > 0)
            {
                Logger.WriteMessage($"    Total Undocumented Tags:  {undocumentedPlatformTags.Length + undocumentedSharedTags.Length}");
            }
        }
    }
}
