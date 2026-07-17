// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class UpdateOptions : Options
{
    /// <summary>
    /// When <c>true</c>, creates the <c>eng/docker-tools</c> directory if it does not already exist.
    /// Otherwise, the command fails when the directory is missing.
    /// </summary>
    public bool Init { get; set; }

    /// <summary>
    /// The fully-qualified ImageBuilder image reference (for example,
    /// <c>mcr.microsoft.com/dotnet-buildtools/image-builder@sha256:...</c> or
    /// <c>mcr.microsoft.com/dotnet-buildtools/image-builder:&lt;tag&gt;</c>) to write into
    /// <c>docker-images.yml</c>. When omitted, the command falls back to the <c>latest</c> reference.
    /// </summary>
    public string? ImageBuilderRef { get; set; }

    private static readonly Option<bool> InitOption = new("--init")
    {
        Description = "Create the eng/docker-tools directory if it does not already exist",
    };

    private static readonly Argument<string> ImageBuilderRefArgument = new(nameof(ImageBuilderRef))
    {
        Description =
            "Fully-qualified ImageBuilder image reference (digest or tag) to record in docker-images.yml. " +
            "Defaults to the 'latest' reference when not specified.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public override IEnumerable<Option> GetCliOptions() =>
        [..base.GetCliOptions(), InitOption];

    public override IEnumerable<Argument> GetCliArguments() =>
        [..base.GetCliArguments(), ImageBuilderRefArgument];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        Init = result.GetValue(InitOption);
        ImageBuilderRef = result.GetValue(ImageBuilderRefArgument);
    }
}
