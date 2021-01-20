// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesOptions : Options, IFilterableOptions, IGitOptionsHost
    {
        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public GitOptions GitOptions { get; } = new GitOptions();

        public string SubscriptionsPath { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
    }

    public class GetStaleImagesOptionsBuilder : CliOptionsBuilder
    {
        private const string DefaultSubscriptionsPath = "subscriptions.json";

        private readonly GitOptionsBuilder _gitOptionsBuilder = new GitOptionsBuilder();
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder =
            new ManifestFilterOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption("subscriptions-path", nameof(GetStaleImagesOptions.SubscriptionsPath),
                            $"Path to the subscriptions file (defaults to '{DefaultSubscriptionsPath}').", DefaultSubscriptionsPath)
                    })
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions())
                .Concat(_gitOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments())
                .Concat(_gitOptionsBuilder.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(GetStaleImagesOptions.VariableName),
                            "The Azure Pipeline variable name to assign the image paths to")
                    });
    }
}
#nullable disable
