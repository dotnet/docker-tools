// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class WaitForMcrDocIngestionOptions : Options
    {
        public string CommitDigest { get; set; } = string.Empty;

        public MarIngestionOptions IngestionOptions { get; set; } = new();
    }

    public class WaitForMcrDocIngestionOptionsBuilder : CliOptionsBuilder
    {
        private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

        private readonly MarIngestionOptionsBuilder _ingestionOptionsBuilder = new();

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._ingestionOptionsBuilder.GetCliArguments(),
                new Argument<string>(nameof(WaitForMcrDocIngestionOptions.CommitDigest),
                    "Git commit digest of the readme changes")
            ];

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._ingestionOptionsBuilder.GetCliOptions(s_defaultTimeout, s_defaultRequeryDelay)
            ];
    }
}
#nullable disable
