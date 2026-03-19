#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CleanAcrImagesOptions : Options
{
    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

    public string RepoName { get; set; }
    public CleanAcrImagesAction Action { get; set; }
    public int Age { get; set; }
    public string RegistryName { get; set; }
    public string[] ImagesToExclude { get; set; } = [];

    private const CleanAcrImagesAction DefaultCleanAcrImagesAction = CleanAcrImagesAction.PruneDangling;
    private const int DefaultAge = 30;

    private static readonly Argument<string> RepoNameArgument = new(nameof(RepoName))
    {
        Description = "Name of repo to target (wildcard chars * and ? supported)"
    };

    private static readonly Argument<string> RegistryNameArgument = new(nameof(RegistryName))
    {
        Description = "Name of the registry"
    };

    private static readonly Option<CleanAcrImagesAction> ActionOption = new(CliHelper.FormatAlias("action"))
    {
        Description = EnumHelper.GetHelpTextOptions(DefaultCleanAcrImagesAction),
        DefaultValueFactory = _ => DefaultCleanAcrImagesAction
    };

    private static readonly Option<int> AgeOption = new(CliHelper.FormatAlias("age"))
    {
        Description = $"Minimum age (days) of repo or images to be deleted (default: {DefaultAge})",
        DefaultValueFactory = _ => DefaultAge
    };

    private static readonly Option<string[]> ImagesToExcludeOption = new(CliHelper.FormatAlias("exclude"))
    {
        Description = $"Name of image to exclude from cleaning (does not apply when using the '{nameof(CleanAcrImagesAction.Delete)}' action)",
        DefaultValueFactory = _ => Array.Empty<string>(),
        AllowMultipleArgumentsPerToken = false
    };

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..CredentialsOptions.GetCliArguments(),
            RepoNameArgument,
            RegistryNameArgument,
        ];

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
            ActionOption,
            AgeOption,
            ImagesToExcludeOption,
        ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        CredentialsOptions.Bind(result);
        RepoName = result.GetValue(RepoNameArgument);
        RegistryName = result.GetValue(RegistryNameArgument);
        Action = result.GetValue(ActionOption);
        Age = result.GetValue(AgeOption);
        ImagesToExclude = result.GetValue(ImagesToExcludeOption) ?? [];
    }
}

// Keep the following templates in sync with these values:
// - eng/pipelines/cleanup-acr-images-custom-official.yml
// - eng/docker-tools/templates/steps/clean-acr-images.yml
public enum CleanAcrImagesAction
{
    /// <summary>
    /// Deletes untagged images in a repo.
    /// </summary>
    PruneDangling,

    /// <summary>
    /// Deletes EOL images in a repo.
    /// Where EOL annotation date is older than the specified age.
    /// </summary>
    PruneEol,

    /// <summary>
    /// Deletes all images in a repo.
    /// </summary>
    PruneAll,

    /// <summary>
    /// Deletes the repo.
    /// </summary>
    Delete
}
