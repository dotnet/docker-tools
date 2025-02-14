// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class SubscriptionOptions
    {
        public string? SubscriptionsPath { get; set; }
    }

    public class SubscriptionOptionsBuilder
    {
        public IEnumerable<Option> GetCliOptions() =>
            new Option[]
            {
                CreateOption<string?>("subscriptions-path", nameof(SubscriptionOptions.SubscriptionsPath),
                    $"Path to the subscriptions file")
            };

        public IEnumerable<Argument> GetCliArguments() => Enumerable.Empty<Argument>();
    }
}
#nullable disable
