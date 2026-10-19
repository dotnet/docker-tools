// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class WaitForMcrDocIngestionOptions : Options
{
    public string CommitDigest { get; set; } = string.Empty;

    public MarIngestionOptions IngestionOptions { get; set; } = new();

    public ServiceConnection? MarServiceConnection { get; set; }

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultRequeryDelay = TimeSpan.FromSeconds(10);

    private static readonly ServiceConnectionOptionsBuilder ServiceConnectionOptionsBuilder = new();

    private static readonly Option<ServiceConnection?> MarServiceConnectionOption =
        ServiceConnectionOptionsBuilder.GetCliOption("--mar-service-connection");

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
            ..IngestionOptions.GetCliOptions(DefaultTimeout, DefaultRequeryDelay),
        ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        IngestionOptions.Bind(result);
        MarServiceConnection = result.GetValue(MarServiceConnectionOption);
        CommitDigest = result.GetValue(CommitDigestArgument) ?? string.Empty;
    }
}
