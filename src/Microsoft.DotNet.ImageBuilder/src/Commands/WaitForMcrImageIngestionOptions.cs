// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

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
                        new Argument<string>(nameof(WaitForMcrImageIngestionOptions.ImageInfoPath), "Path to image info file")
                    }
                )
                .Concat(ServicePrincipalOptions.GetCliArguments());

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        new Option<DateTime>("--min-queue-time",
                            description: "Minimum queue time an image must have to be awaited",
                            parseArgument: resultArg => DateTime.Parse(resultArg.Tokens.First().Value).ToUniversalTime())
                        {
                            Argument = new Argument<DateTime>(() => DateTime.MinValue),
                            Name = nameof(WaitForMcrImageIngestionOptions.MinimumQueueTime)
                        },
                        new Option<TimeSpan>("--timeout",
                            description: $"Maximum time to wait for image ingestion (default: {DefaultWaitTimeout})",
                            parseArgument: resultArg => TimeSpan.Parse(resultArg.Tokens.First().Value))
                        {
                            Argument = new Argument<TimeSpan>(() => DefaultWaitTimeout),
                            Name = nameof(WaitForMcrImageIngestionOptions.WaitTimeout)
                        },
                        new Option<TimeSpan>("--requery-delay",
                            description: $"Amount of time to wait before requerying the status of an image (default: {DefaultRequeryDelay})",
                            parseArgument: resultArg => TimeSpan.Parse(resultArg.Tokens.First().Value))
                        {
                            Argument = new Argument<TimeSpan>(() => DefaultRequeryDelay),
                            Name = nameof(WaitForMcrImageIngestionOptions.RequeryDelay)
                        }
                    });
    }
}
#nullable disable
