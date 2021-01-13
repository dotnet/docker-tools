// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class QueueBuildOptions : Options
    {
        public string SubscriptionsPath { get; set; } = string.Empty;
        public AzdoOptions AzdoOptions { get; set; } = new AzdoOptions();
        public IEnumerable<string> AllSubscriptionImagePaths { get; set; } = Enumerable.Empty<string>();
    }

    public class QueueBuildSymbolsBuilder : CliSymbolsBuilder
    {
        private const string DefaultSubscriptionsPath = "subscriptions.json";
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(AzdoOptions.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        new Option<string>("--subscriptions-path", () => DefaultSubscriptionsPath,
                            $"Path to the subscriptions file (defaults to '{DefaultSubscriptionsPath}').")
                        {
                            Name = nameof(QueueBuildOptions.SubscriptionsPath)
                        },
                        new Option<string[]>("--image-paths", () => Array.Empty<string>(),
                            "JSON string mapping a subscription ID to the image paths to be built (from the output variable of getStaleImages)")
                        {
                            Name = nameof(QueueBuildOptions.AllSubscriptionImagePaths)
                        }
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments().Concat(AzdoOptions.GetCliArguments());
    }
}
#nullable disable
