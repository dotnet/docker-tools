// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateEolAnnotationDataOptions : Options
{
    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();
    public RegistryOptions RegistryOptions { get; set; } = new();
    public ServiceConnection? AcrServiceConnection { get; set; }
    public string EolDigestsListPath { get; set; } = string.Empty;

    private static readonly RegistryOptionsBuilder RegistryBuilder = new(isOverride: false);
    private static readonly ServiceConnectionOptionsBuilder ServiceConnectionBuilder = new();

    private static readonly Option<ServiceConnection?> AcrServiceConnectionOption =
        ServiceConnectionBuilder.GetCliOption("acr-service-connection");

    private static readonly Argument<string> EolDigestsListPathArgument = new(nameof(EolDigestsListPath))
    {
        Description = "EOL annotations digests list output path"
    };

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        ..CredentialsOptions.GetCliOptions(),
        AcrServiceConnectionOption,
    ];

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        ..CredentialsOptions.GetCliArguments(),
        ..RegistryBuilder.GetCliArguments(),
        EolDigestsListPathArgument,
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        CredentialsOptions.Bind(result);
        RegistryBuilder.Bind(result, RegistryOptions);
        AcrServiceConnection = result.GetValue(AcrServiceConnectionOption);
        EolDigestsListPath = result.GetValue(EolDigestsListPathArgument) ?? string.Empty;
    }
}
