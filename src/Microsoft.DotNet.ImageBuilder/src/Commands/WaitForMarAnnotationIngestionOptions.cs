// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMarAnnotationIngestionOptions : Options
    {
        public string EolDigestsListPath { get; set; } = string.Empty;

        public MarIngestionOptions IngestionOptions { get; set; } = new();
    }

    public class WaitForMarAnnotationIngestionOptionsBuilder : CliOptionsBuilder
    {
        private static readonly TimeSpan s_defaultWaitTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

        private readonly MarIngestionOptionsBuilder _ingestionOptionsBuilder = new();

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._ingestionOptionsBuilder.GetCliArguments(),
                new Argument<string>(nameof(WaitForMarAnnotationIngestionOptions.EolDigestsListPath),
                    "EOL annotations digests list path")
            ];

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._ingestionOptionsBuilder.GetCliOptions(s_defaultWaitTimeout, s_defaultRequeryDelay)
            ];
    }
}
