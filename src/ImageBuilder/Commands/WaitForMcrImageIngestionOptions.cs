// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMcrImageIngestionOptions : ManifestOptions
    {
        public string ImageInfoPath { get; set; } = string.Empty;

        public DateTime MinimumQueueTime { get; set; }

        public MarIngestionOptions IngestionOptions { get; set; } = new();

        public ServiceConnection? MarServiceConnection { get; set; }

        private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan DefaultRequeryDelay = TimeSpan.FromSeconds(10);

        private static readonly ServiceConnectionOptionsBuilder ServiceConnectionBuilder = new();

        private static readonly Option<ServiceConnection?> MarServiceConnectionOption =
            ServiceConnectionBuilder.GetCliOption("mar-service-connection");

        private static readonly Option<DateTime> MinQueueTimeOption = new(CliHelper.FormatAlias("min-queue-time"))
        {
            Description = "Minimum queue time an image must have to be awaited",
            DefaultValueFactory = _ => DateTime.MinValue,
            CustomParser = argResult => DateTime.Parse(argResult.GetTokenValue()).ToUniversalTime()
        };

        private static readonly Argument<string> ImageInfoPathArgument = new(nameof(ImageInfoPath))
        {
            Description = "Path to image info file"
        };

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..IngestionOptions.GetCliArguments(),
            ImageInfoPathArgument,
        ];

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..IngestionOptions.GetCliOptions(DefaultWaitTimeout, DefaultRequeryDelay),
            MarServiceConnectionOption,
            MinQueueTimeOption,
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            IngestionOptions.Bind(result);
            MarServiceConnection = result.GetValue(MarServiceConnectionOption);
            MinimumQueueTime = result.GetValue(MinQueueTimeOption);
            ImageInfoPath = result.GetValue(ImageInfoPathArgument) ?? string.Empty;
        }
    }
}
