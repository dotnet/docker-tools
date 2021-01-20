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
    public class QueueBuildOptions : Options
    {
        public string SubscriptionsPath { get; set; } = string.Empty;
        public AzdoOptions AzdoOptions { get; set; } = new AzdoOptions();
        public IEnumerable<string> AllSubscriptionImagePaths { get; set; } = Enumerable.Empty<string>();
    }

    public class QueueBuildOptionsBuilder : CliOptionsBuilder
    {
        private readonly AzdoOptionsBuilder _azdoOptionsBuilder = new AzdoOptionsBuilder();

        private const string DefaultSubscriptionsPath = "subscriptions.json";
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_azdoOptionsBuilder.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        CreateOption("subscriptions-path", nameof(QueueBuildOptions.SubscriptionsPath),
                            $"Path to the subscriptions file (defaults to '{DefaultSubscriptionsPath}').", DefaultSubscriptionsPath),
                        CreateMultiOption<string>("image-paths", nameof(QueueBuildOptions.AllSubscriptionImagePaths),
                            "JSON string mapping a subscription ID to the image paths to be built (from the output variable of getStaleImages)")
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments().Concat(_azdoOptionsBuilder.GetCliArguments());
    }
}
#nullable disable
