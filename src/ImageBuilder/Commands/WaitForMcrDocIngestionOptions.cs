// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class WaitForMcrDocIngestionOptions : Options
{
    public string CommitDigest { get; set; } = string.Empty;

    public MarIngestionOptions IngestionOptions { get; set; } = new();

    public ServiceConnection? MarServiceConnection { get; set; }

    private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

    private static readonly ServiceConnectionOptionsBuilder s_serviceConnectionOptionsBuilder = new();

    private static readonly Option<ServiceConnection?> MarServiceConnectionOption =
        s_serviceConnectionOptionsBuilder.GetCliOption("mar-service-connection");

    private static readonly Argument<string> CommitDigestArgument = new(nameof(CommitDigest))
    {
        Description = "Git commit digest of the readme changes"
    };

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..IngestionOptions.GetCliArguments(),
            CommitDigestArgument,
        ];

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            MarServiceConnectionOption,
            ..IngestionOptions.GetCliOptions(s_defaultTimeout, s_defaultRequeryDelay),
        ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        IngestionOptions.Bind(result);
        MarServiceConnection = result.GetValue(MarServiceConnectionOption);
        CommitDigest = result.GetValue(CommitDigestArgument) ?? string.Empty;
    }
}
