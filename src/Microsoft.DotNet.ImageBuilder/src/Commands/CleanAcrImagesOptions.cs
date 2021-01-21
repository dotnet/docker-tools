﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CleanAcrImagesOptions : Options
    {
        public string RepoName { get; set; }
        public CleanAcrImagesAction Action { get; set; }
        public int Age { get; set; }
        public ServicePrincipalOptions ServicePrincipal { get; set; } = new ServicePrincipalOptions();
        public string Subscription { get; set; }
        public string ResourceGroup { get; set; }
        public string RegistryName { get; set; }
    }

    public class CleanAcrImagesOptionsBuilder : CliOptionsBuilder
    {
        private const CleanAcrImagesAction DefaultCleanAcrImagesAction = CleanAcrImagesAction.PruneDangling;
        private const int DefaultAge = 30;

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(CleanAcrImagesOptions.RepoName),
                            "Name of repo to target (wildcard chars * and ? supported)"),
                    })
                .Concat(ServicePrincipalOptions.GetCliArguments())
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
            base.GetCliOptions().Concat(
                new Option[]
                {
                    CreateOption("action", nameof(CleanAcrImagesOptions.Action),
                        EnumHelper.GetHelpTextOptions(DefaultCleanAcrImagesAction), DefaultCleanAcrImagesAction),
                    CreateOption("age", nameof(CleanAcrImagesOptions.Age),
                        $"Minimum age (days) of repo or images to be deleted (default: {DefaultAge})", DefaultAge)
                });
    }

    public enum CleanAcrImagesAction
    {
        /// <summary>
        /// Deletes untagged images in a repo.
        /// </summary>
        PruneDangling,

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
