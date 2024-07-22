// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMcrImageIngestionOptions : ManifestOptions
    {
        public string ImageInfoPath { get; set; } = string.Empty;

        public DateTime MinimumQueueTime { get; set; }

        public MarIngestionOptions IngestionOptions { get; set; } = new();
    }

    public class WaitForMcrImageIngestionOptionsBuilder : ManifestOptionsBuilder
    {
        private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan DefaultRequeryDelay = TimeSpan.FromSeconds(10);

        private readonly MarIngestionOptionsBuilder _ingestionOptionsBuilder = new();

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_ingestionOptionsBuilder.GetCliArguments())
                .Concat(
                    [
                        new Argument<string>(nameof(WaitForMcrImageIngestionOptions.ImageInfoPath),
                            "Path to image info file")
                    ]
                );

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_ingestionOptionsBuilder.GetCliOptions(DefaultWaitTimeout, DefaultRequeryDelay))
                .Concat(
                    [
                        CreateOption("min-queue-time", nameof(WaitForMcrImageIngestionOptions.MinimumQueueTime),
                            "Minimum queue time an image must have to be awaited",
                            val => DateTime.Parse(val).ToUniversalTime(), DateTime.MinValue)
                    ]);
    }
}
