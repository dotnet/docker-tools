// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateEolAnnotationDataForPublishOptions : GenerateEolAnnotationDataOptions
{
    public string OldImageInfoPath { get; set; } = string.Empty;
    public string NewImageInfoPath { get; set; } = string.Empty;
}

public class GenerateEolAnnotationDataOptionsForPublishBuilder : GenerateEolAnnotationDataOptionsBuilder
{
    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            new Argument<string>(nameof(GenerateEolAnnotationDataForPublishOptions.OldImageInfoPath),
                "Old image-info file"),
            new Argument<string>(nameof(GenerateEolAnnotationDataForPublishOptions.NewImageInfoPath),
                "New image-info file"),
        ];
}
