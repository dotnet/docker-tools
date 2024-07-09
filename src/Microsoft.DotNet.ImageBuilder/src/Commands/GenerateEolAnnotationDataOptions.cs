// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateEolAnnotationDataOptions : Options
{
    public string EolDigestsListPath { get; set; } = string.Empty;
    public string OldImageInfoPath { get; set; } = string.Empty;
    public string NewImageInfoPath { get; set; } = string.Empty;
    public bool AnnotateEolProducts { get; set; }
    public string RepoPrefix { get; set; } = string.Empty;
    public int LogsQueryDayRange { get; set; }
    public string LogsWorkspaceId { get; set; } = string.Empty;
}

public class GenerateEolAnnotationDataOptionsBuilder : CliOptionsBuilder
{
    public override IEnumerable<Option> GetCliOptions() =>
        base.GetCliOptions()
            .Concat(
                [
                    CreateOption<bool>("annotate-eol-products", nameof(GenerateEolAnnotationDataOptions.AnnotateEolProducts),
                        "Annotate images of EOL products"),
                    CreateOption("log-days", nameof(GenerateEolAnnotationDataOptions.LogsQueryDayRange),
                        "Number of days in the past to query for previous images", 7),
                ]
            );

    public override IEnumerable<Argument> GetCliArguments() =>
        base.GetCliArguments()
            .Concat(
                [
                    new Argument<string>(nameof(GenerateEolAnnotationDataOptions.OldImageInfoPath),
                        "Old image-info file"),
                    new Argument<string>(nameof(GenerateEolAnnotationDataOptions.NewImageInfoPath),
                        "New image-info file"),
                    new Argument<string>(nameof(AnnotateEolDigestsOptions.EolDigestsListPath),
                        "Eol annotations digests list path"),
                    new Argument<string>(nameof(GenerateEolAnnotationDataOptions.RepoPrefix),
                        "Prefix to add to the repo names specified in the manifest"),
                    new Argument<string>(nameof(GenerateEolAnnotationDataOptions.LogsWorkspaceId),
                        "ID of the log analytics workspace containing the ACR logs"),
                ]
            );
}
