// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

#nullable enable
public class GenerateSigningPayloadsOptions : Options
{
    public string? ImageInfoPath { get; set; }
    public string? PayloadOutputDirectory { get; set; }
}

public class GenerateSigningPayloadsOptionsBuilder : CliOptionsBuilder
{
    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            new Argument<string>(
                name: nameof(GenerateSigningPayloadsOptions.ImageInfoPath),
                description: "Image info file to generate payloads for"),
            new Argument<string>(
                name: nameof(GenerateSigningPayloadsOptions.PayloadOutputDirectory),
                description: "Directory where signing payloads will be placed"),
        ];
}
