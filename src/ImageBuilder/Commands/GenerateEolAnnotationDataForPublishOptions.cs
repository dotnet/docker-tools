// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateEolAnnotationDataForPublishOptions : GenerateEolAnnotationDataOptions
{
    public string OldImageInfoPath { get; set; } = string.Empty;
    public string NewImageInfoPath { get; set; } = string.Empty;

    private static readonly Argument<string> OldImageInfoPathArgument = new(nameof(OldImageInfoPath))
    {
        Description = "Old image-info file"
    };

    private static readonly Argument<string> NewImageInfoPathArgument = new(nameof(NewImageInfoPath))
    {
        Description = "New image-info file"
    };

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        OldImageInfoPathArgument,
        NewImageInfoPathArgument,
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        OldImageInfoPath = result.GetValue(OldImageInfoPathArgument) ?? string.Empty;
        NewImageInfoPath = result.GetValue(NewImageInfoPathArgument) ?? string.Empty;
    }
}
