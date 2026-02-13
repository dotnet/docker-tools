// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

public class SignImagesOptions : Options
{
    public string ImageInfoPath { get; set; } = string.Empty;
}

public class SignImagesOptionsBuilder : CliOptionsBuilder
{
    public override IEnumerable<Argument> GetCliArguments() =>
        base.GetCliArguments()
            .Concat(
            [
                new Argument<string>(nameof(SignImagesOptions.ImageInfoPath),
                    "Path to merged image info file containing images to sign")
            ]);
}
