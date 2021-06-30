// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesOptions : Options, IFilterableOptions, IGitOptionsHost
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public GitOptions GitOptions { get; set; } = new GitOptions();

        public SubscriptionOptions SubscriptionOptions { get; set; } = new SubscriptionOptions();

        public string VariableName { get; set; } = string.Empty;
    }

    public class GetStaleImagesOptionsBuilder : CliOptionsBuilder
    {
        private readonly GitOptionsBuilder _gitOptionsBuilder = GitOptionsBuilder.BuildWithDefaults();
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder =
            new ManifestFilterOptionsBuilder();
        private readonly SubscriptionOptionsBuilder _subscriptionOptionsBuilder = new SubscriptionOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_subscriptionOptionsBuilder.GetCliOptions())
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions())
                .Concat(_gitOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_subscriptionOptionsBuilder.GetCliArguments())
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
