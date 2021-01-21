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
    public class WaitForMcrDocIngestionOptions : Options
    {
        public string CommitDigest { get; set; } = string.Empty;

        public TimeSpan WaitTimeout { get; set; }

        public TimeSpan RequeryDelay { get; set; }

        public ServicePrincipalOptions ServicePrincipal { get; set; } = new ServicePrincipalOptions();
    }

    public class WaitForMcrDocIngestionOptionsBuilder : CliOptionsBuilder
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultRequeryDelay = TimeSpan.FromSeconds(10);

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(WaitForMcrDocIngestionOptions.CommitDigest),
                            "Git commit digest of the readme changes")
                    }
                )
                .Concat(ServicePrincipalOptions.GetCliArguments());

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption("timeout", nameof(WaitForMcrDocIngestionOptions.WaitTimeout),
                            $"Maximum time to wait for doc ingestion (default: {DefaultTimeout})",
                            val => TimeSpan.Parse(val), DefaultTimeout),
                        CreateOption("requery-delay", nameof(WaitForMcrDocIngestionOptions.RequeryDelay),
                            $"Amount of time to wait before requerying the status of the commit (default: {DefaultRequeryDelay})",
                            val => TimeSpan.Parse(val), DefaultRequeryDelay)
                    }
                );
    }
}
#nullable disable
