// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class WaitForMarAnnotationIngestionOptions : Options
{
    public string AnnotationDigestsPath { get; set; } = string.Empty;

    public MarIngestionOptions IngestionOptions { get; set; } = new();

    public ServiceConnection? MarServiceConnection { get; set; }

    private static readonly TimeSpan s_defaultWaitTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

    private static readonly ServiceConnectionOptionsBuilder s_serviceConnectionOptionsBuilder = new();

    private static readonly Option<ServiceConnection?> MarServiceConnectionOption =
        s_serviceConnectionOptionsBuilder.GetCliOption("mar-service-connection");

    private static readonly Argument<string> AnnotationDigestsPathArgument = new(nameof(AnnotationDigestsPath))
    {
        Description = "Path of file containing the list of annotation digests to be queried"
    };

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..IngestionOptions.GetCliArguments(),
            AnnotationDigestsPathArgument,
        ];

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..IngestionOptions.GetCliOptions(s_defaultWaitTimeout, s_defaultRequeryDelay),
            MarServiceConnectionOption,
        ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        IngestionOptions.Bind(result);
        MarServiceConnection = result.GetValue(MarServiceConnectionOption);
        AnnotationDigestsPath = result.GetValue(AnnotationDigestsPathArgument) ?? string.Empty;
    }
}
