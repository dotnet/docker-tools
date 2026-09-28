// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Options shared by all <see cref="AttachLifecycleMetadataCommand"/> subcommands.
/// </summary>
public class AttachLifecycleMetadataOptions : Options
{
    public RegistryOptions RegistryOptions { get; set; } = new();

    private readonly RegistryOptionsBuilder _registryOptionsBuilder = new(isOverride: false);

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        .._registryOptionsBuilder.GetCliArguments(),
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        _registryOptionsBuilder.Bind(result, RegistryOptions);
    }
}

public class AttachPublishedLifecycleMetadataOptions : AttachLifecycleMetadataOptions
{
    public string OldImageInfoPath { get; set; } = string.Empty;
    public string NewImageInfoPath { get; set; } = string.Empty;
    public bool WaitForIngestion { get; set; }
    public MarIngestionOptions IngestionOptions { get; set; } = new();
    public ServiceConnection? MarServiceConnection { get; set; }

    private static readonly TimeSpan s_defaultWaitTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

    private static readonly Argument<string> s_oldImageInfoPathArgument = new(nameof(OldImageInfoPath))
    {
        Description = "Previously published image info file"
    };

    private static readonly Argument<string> s_newImageInfoPathArgument = new(nameof(NewImageInfoPath))
    {
        Description = "Image info file describing the currently supported images"
    };

    private static readonly Option<bool> s_waitForIngestionOption = new("--wait-for-ingestion")
    {
        Description = "Wait for the created annotations to be ingested by MAR"
    };

    private static readonly Option<ServiceConnection?> s_marServiceConnectionOption =
        new ServiceConnectionOptionsBuilder().GetCliOption("--mar-service-connection");

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        s_oldImageInfoPathArgument,
        s_newImageInfoPathArgument,
    ];

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        s_waitForIngestionOption,
        ..IngestionOptions.GetCliOptions(s_defaultWaitTimeout, s_defaultRequeryDelay),
        s_marServiceConnectionOption,
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        OldImageInfoPath = result.GetValue(s_oldImageInfoPathArgument) ?? string.Empty;
        NewImageInfoPath = result.GetValue(s_newImageInfoPathArgument) ?? string.Empty;
        WaitForIngestion = result.GetValue(s_waitForIngestionOption);
        IngestionOptions.Bind(result);
        MarServiceConnection = result.GetValue(s_marServiceConnectionOption);
    }
}
