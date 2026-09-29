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
public class LifecycleMetadataOptions : Options
{
    public bool MarkAsInternal { get; set; }
    public LifecycleMetadataConflictAction OnConflict { get; set; }
    public bool WaitForIngestion { get; set; }
    public MarIngestionOptions IngestionOptions { get; set; } = new();
    public ServiceConnection? MarServiceConnection { get; set; }

    private static readonly TimeSpan s_defaultWaitTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

    private static readonly Option<bool> s_waitForIngestionOption = new("--wait-for-ingestion")
    {
        Description = "Wait for the created annotations to be ingested by MAR"
    };

    private static readonly Option<ServiceConnection?> s_marServiceConnectionOption =
        new ServiceConnectionOptionsBuilder().GetCliOption("--mar-service-connection");

    private static readonly Option<LifecycleMetadataConflictAction> s_onConflictOption = new("--on-conflict")
    {
        Description = "Action when existing lifecycle metadata has a different EOL date (error or skip)",
        DefaultValueFactory = _ => LifecycleMetadataConflictAction.Error
    };

    private static readonly Option<bool> s_markAsInternalOption = new("--mark-as-internal")
    {
        Description = "Mark lifecycle metadata as internal-only so it is never copied when publishing"
    };

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        s_markAsInternalOption,
        s_onConflictOption,
        s_waitForIngestionOption,
        ..IngestionOptions.GetCliOptions(s_defaultWaitTimeout, s_defaultRequeryDelay),
        s_marServiceConnectionOption,
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        MarkAsInternal = result.GetValue(s_markAsInternalOption);
        OnConflict = result.GetValue(s_onConflictOption);
        WaitForIngestion = result.GetValue(s_waitForIngestionOption);
        IngestionOptions.Bind(result);
        MarServiceConnection = result.GetValue(s_marServiceConnectionOption);
    }
}

public class RegistryLifecycleMetadataOptions : LifecycleMetadataOptions
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

public class FileLifecycleMetadataOptions : LifecycleMetadataOptions
{
    public string EolDigestsListPath { get; set; } = string.Empty;

    private static readonly Argument<string> s_eolDigestsListPathArgument = new(nameof(EolDigestsListPath))
    {
        Description = "JSON file containing fully-qualified image digests and their EOL dates"
    };

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        s_eolDigestsListPathArgument,
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        EolDigestsListPath = result.GetValue(s_eolDigestsListPathArgument) ?? string.Empty;
    }
}

public class UnsupportedLifecycleMetadataOptions : RegistryLifecycleMetadataOptions
{
    public string OldImageInfoPath { get; set; } = string.Empty;
    public string NewImageInfoPath { get; set; } = string.Empty;

    private static readonly Argument<string> s_oldImageInfoPathArgument = new(nameof(OldImageInfoPath))
    {
        Description = "Previously published image info file"
    };

    private static readonly Argument<string> s_newImageInfoPathArgument = new(nameof(NewImageInfoPath))
    {
        Description = "Image info file describing the currently supported images"
    };

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        s_oldImageInfoPathArgument,
        s_newImageInfoPathArgument,
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        OldImageInfoPath = result.GetValue(s_oldImageInfoPathArgument) ?? string.Empty;
        NewImageInfoPath = result.GetValue(s_newImageInfoPathArgument) ?? string.Empty;
    }
}
