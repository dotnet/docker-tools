// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Configuration;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMarAnnotationIngestionOptions : Options
    {
        public string AnnotationDigestsPath { get; set; } = string.Empty;

        public MarIngestionOptions IngestionOptions { get; set; } = new();

        public ServiceConnectionOptions? MarServiceConnection { get; set; }
    }

    public class WaitForMarAnnotationIngestionOptionsBuilder : CliOptionsBuilder
    {
        private static readonly TimeSpan s_defaultWaitTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

        private readonly MarIngestionOptionsBuilder _ingestionOptionsBuilder = new();
        private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._ingestionOptionsBuilder.GetCliArguments(),
                new Argument<string>(nameof(WaitForMarAnnotationIngestionOptions.AnnotationDigestsPath),
                    "Path of file containing the list of annotation digests to be queried"),
            ];

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._ingestionOptionsBuilder.GetCliOptions(s_defaultWaitTimeout, s_defaultRequeryDelay),
                .._serviceConnectionOptionsBuilder.GetCliOptions(
                    "mar-service-connection",
                    nameof(WaitForMarAnnotationIngestionOptions.MarServiceConnection)),
            ];
    }
}
