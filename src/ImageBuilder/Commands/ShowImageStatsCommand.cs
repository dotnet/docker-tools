// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class ShowImageStatsCommand(IManifestJsonService manifestJsonService, ILogger<ShowImageStatsCommand> logger) : ManifestCommand<ShowImageStatsOptions>(manifestJsonService)
{
    private readonly ILogger<ShowImageStatsCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override string Description => "Displays statistics about the number of images";

    public override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IMAGE STATISTICS");

        PlatformInfo[] platforms = Manifest.GetFilteredPlatforms().ToArray();
        LogGeneralStats(platforms);
        LogBaseImageStats(platforms);

        return Task.CompletedTask;
    }

    private static string FormatBaseImageStats(
        string baseImage,
        object dependentImages,
        object dependentSimpleTags,
        object dependentSharedTags,
        object dependentTags)
        => $"{baseImage,-50}  {dependentImages,17}  {dependentSimpleTags,22}  {dependentSharedTags,21}  {dependentTags,20}";

    private void LogBaseImageStats(PlatformInfo[] platforms)
    {
        IEnumerable<string> externalBaseImages = platforms
            .SelectMany(platform => platform.ExternalFromImages)
            .Distinct()
            .OrderBy(name => name)
            .Select(name => $"{name}");

        _logger.LogInformation(
            "{BaseImageStats}",
            FormatBaseImageStats(
                $"External Base Images ({externalBaseImages.Count()})",
                "Dependent Images",
                "Dependent Simple Tags",
                "Dependent Shared Tags",
                "Total Dependent Tags"));

        foreach (string baseImage in externalBaseImages)
        {
            PlatformInfo[] dependentPlatforms = platforms
                .Where(platform => platform.ExternalFromImages.Contains(baseImage))
                .SelectMany(platform => Manifest.GetDescendants(platform, Manifest.GetAllPlatforms().ToList()))
                .Distinct()
                .ToArray();
            int dependentPlatformTagsCount = dependentPlatforms.SelectMany(platform => platform.Tags).Count();
            int dependentSharedTagsCount = Manifest.GetFilteredImages()
                .Where(image => image.FilteredPlatforms.Intersect(dependentPlatforms).Any())
                .SelectMany(image => image.SharedTags)
                .Count();

            _logger.LogInformation(
                "{BaseImageStats}",
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
        TagInfo[] undocumentedPlatformTags = platformTags.Where(tag => tag.Model.DocType == TagDocumentationType.Undocumented).ToArray();
        TagInfo[] undocumentedSharedTags = sharedTags.Where(tag => tag.Model.DocType == TagDocumentationType.Undocumented).ToArray();

        _logger.LogInformation("Total Unique Images:  {UniqueImageCount}", platforms.Length);
        _logger.LogInformation("Total Simple Tags:  {SimpleTagCount}", platformTags.Length);

        if (undocumentedPlatformTags.Length > 0)
        {
            _logger.LogInformation(
                "    Total Undocumented Simple Tags:  {UndocumentedSimpleTagCount}",
                undocumentedPlatformTags.Length);
        }

        _logger.LogInformation("Total Shared Tags:  {SharedTagCount}", sharedTags.Length);

        if (undocumentedSharedTags.Length > 0)
        {
            _logger.LogInformation(
                "    Total Undocumented Shared Tags:  {UndocumentedSharedTagCount}",
                undocumentedSharedTags.Length);
        }

        _logger.LogInformation("Total Tags:  {TagCount}", platformTags.Length + sharedTags.Length);

        if (undocumentedPlatformTags.Length > 0 && undocumentedSharedTags.Length > 0)
        {
            _logger.LogInformation(
                "    Total Undocumented Tags:  {UndocumentedTagCount}",
                undocumentedPlatformTags.Length + undocumentedSharedTags.Length);
        }
    }
}
