﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.DockerTools.ImageBuilder;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class CleanAcrImagesOptions : Options
    {
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

        public string RepoName { get; set; }
        public CleanAcrImagesAction Action { get; set; }
        public int Age { get; set; }
        public string Subscription { get; set; }
        public string ResourceGroup { get; set; }
        public string RegistryName { get; set; }
        public string[] ImagesToExclude { get; set; } = [];
    }

    public class CleanAcrImagesOptionsBuilder : CliOptionsBuilder
    {
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();

        private const CleanAcrImagesAction DefaultCleanAcrImagesAction = CleanAcrImagesAction.PruneDangling;
        private const int DefaultAge = 30;

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_registryCredentialsOptionsBuilder.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(CleanAcrImagesOptions.RepoName),
                            "Name of repo to target (wildcard chars * and ? supported)"),
                    })
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(CleanAcrImagesOptions.Subscription),
                            "Azure subscription to operate on"),
                        new Argument<string>(nameof(CleanAcrImagesOptions.ResourceGroup),
                            "Azure resource group to operate on"),
                        new Argument<string>(nameof(CleanAcrImagesOptions.RegistryName),
                            "Name of the registry"),
                    });

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_registryCredentialsOptionsBuilder.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        CreateOption("action", nameof(CleanAcrImagesOptions.Action),
                            EnumHelper.GetHelpTextOptions(DefaultCleanAcrImagesAction), DefaultCleanAcrImagesAction),
                        CreateOption("age", nameof(CleanAcrImagesOptions.Age),
                            $"Minimum age (days) of repo or images to be deleted (default: {DefaultAge})", DefaultAge),
                        CreateMultiOption<string>("exclude", nameof(CleanAcrImagesOptions.ImagesToExclude),
                            $"Name of image to exclude from cleaning (does not apply when using the '{nameof(CleanAcrImagesAction.Delete)}' action)"),
                    });
    }

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
}
