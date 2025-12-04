// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Configuration;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMcrImageIngestionOptions : ManifestOptions
    {
        public string ImageInfoPath { get; set; } = string.Empty;

        public DateTime MinimumQueueTime { get; set; }

        public MarIngestionOptions IngestionOptions { get; set; } = new();

        public ServiceConnectionOptions? MarServiceConnection { get; set; }
    }

    public class WaitForMcrImageIngestionOptionsBuilder : ManifestOptionsBuilder
    {
        private static readonly TimeSpan s_defaultWaitTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan s_defaultRequeryDelay = TimeSpan.FromSeconds(10);

        private readonly MarIngestionOptionsBuilder _ingestionOptionsBuilder = new();
        private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._ingestionOptionsBuilder.GetCliArguments(),
                new Argument<string>(nameof(WaitForMcrImageIngestionOptions.ImageInfoPath),
                    "Path to image info file")
            ];

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._ingestionOptionsBuilder.GetCliOptions(s_defaultWaitTimeout, s_defaultRequeryDelay),
                .._serviceConnectionOptionsBuilder.GetCliOptions(
                    "mar-service-connection",
                    nameof(WaitForMcrImageIngestionOptions.MarServiceConnection)),
                CreateOption("min-queue-time", nameof(WaitForMcrImageIngestionOptions.MinimumQueueTime),
                    "Minimum queue time an image must have to be awaited",
                    val => DateTime.Parse(val).ToUniversalTime(), DateTime.MinValue)
            ];
    }
}
