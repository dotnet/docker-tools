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

    private static readonly Option<bool> InitOption = new("--init")
    {
        Description = "Create the eng/docker-tools directory if it does not already exist",
    };

    public override IEnumerable<Option> GetCliOptions() =>
        [..base.GetCliOptions(), InitOption];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        Init = result.GetValue(InitOption);
    }
}
