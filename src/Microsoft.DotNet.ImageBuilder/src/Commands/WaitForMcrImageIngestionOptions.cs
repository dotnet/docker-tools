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

        public TimeSpan WaitTimeout { get; set; }

        public TimeSpan RequeryDelay { get; set; }

        public ServicePrincipalOptions ServicePrincipal { get; } = new ServicePrincipalOptions();
    }

    public class WaitForMcrImageIngestionSymbolsBuilder : ManifestSymbolsBuilder
    {
        private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan DefaultRequeryDelay = TimeSpan.FromSeconds(10);

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(WaitForMcrImageIngestionOptions.ImageInfoPath),
                            "Path to image info file")
                    }
                )
                .Concat(ServicePrincipalOptions.GetCliArguments());

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption("min-queue-time", nameof(WaitForMcrImageIngestionOptions.MinimumQueueTime),
                            "Minimum queue time an image must have to be awaited",
                            val => DateTime.Parse(val).ToUniversalTime(), DateTime.MinValue),
                        CreateOption("timeout", nameof(WaitForMcrImageIngestionOptions.WaitTimeout),
                            $"Maximum time to wait for image ingestion (default: {DefaultWaitTimeout})",
                            val => TimeSpan.Parse(val), DefaultWaitTimeout),
                        CreateOption("--requery-delay", nameof(WaitForMcrImageIngestionOptions.RequeryDelay),
                            $"Amount of time to wait before requerying the status of an image (default: {DefaultRequeryDelay})",
                            val => TimeSpan.Parse(val), DefaultRequeryDelay)
                    });
    }
}
#nullable disable
