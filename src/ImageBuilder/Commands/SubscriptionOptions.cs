// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class SubscriptionOptions
{
    public string? SubscriptionsPath { get; set; }

    private static readonly Option<string?> SubscriptionsPathOption = new(CliHelper.FormatAlias("subscriptions-path"))
    {
        Description = "Path to the subscriptions file"
    };

    public IEnumerable<Option> GetCliOptions() =>
        [SubscriptionsPathOption];

    public IEnumerable<Argument> GetCliArguments() => [];

    public void Bind(ParseResult result)
    {
        SubscriptionsPath = result.GetValue(SubscriptionsPathOption);
    }
}
