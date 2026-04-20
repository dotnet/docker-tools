// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class AnnotateEolDigestsOptions : Options
{
    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

    public string EolDigestsListPath { get; set; } = string.Empty;
    public string AcrName { get; set; } = string.Empty;
    public string RepoPrefix { get; set; } = string.Empty;
    public string AnnotationDigestsOutputPath { get; set; } = string.Empty;

    private static readonly Argument<string> EolDigestsListPathArgument = new(nameof(EolDigestsListPath))
    {
        Description = "EOL annotations digests list path"
    };

    private static readonly Argument<string> AcrNameArgument = new(nameof(AcrName))
    {
        Description = "Azure registry name"
    };

    private static readonly Argument<string> RepoPrefixArgument = new(nameof(RepoPrefix))
    {
        Description = "Publish prefix of the repo names"
    };

    private static readonly Argument<string> AnnotationDigestsOutputPathArgument = new(nameof(AnnotationDigestsOutputPath))
    {
        Description = "Output path of file containing the list of annotation digests that were created"
    };

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
        ];

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..CredentialsOptions.GetCliArguments(),
            EolDigestsListPathArgument,
            AcrNameArgument,
            RepoPrefixArgument,
            AnnotationDigestsOutputPathArgument,
        ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        CredentialsOptions.Bind(result);
        EolDigestsListPath = result.GetValue(EolDigestsListPathArgument) ?? string.Empty;
        AcrName = result.GetValue(AcrNameArgument) ?? string.Empty;
        RepoPrefix = result.GetValue(RepoPrefixArgument) ?? string.Empty;
        AnnotationDigestsOutputPath = result.GetValue(AnnotationDigestsOutputPathArgument) ?? string.Empty;
    }
}
