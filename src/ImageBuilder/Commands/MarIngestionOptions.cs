// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable
public class MarIngestionOptions
{
    public TimeSpan WaitTimeout { get; set; }

    public TimeSpan RequeryDelay { get; set; }
}

internal class MarIngestionOptionsBuilder
{
    public IEnumerable<Option> GetCliOptions(TimeSpan defaultTimeout, TimeSpan defaultRequeryDelay) =>
        [
            CreateOption("timeout", nameof(MarIngestionOptions.WaitTimeout),
                $"Maximum time to wait for ingestion",
                val => TimeSpan.Parse(val), defaultTimeout),
            CreateOption("requery-delay", nameof(MarIngestionOptions.RequeryDelay),
                $"Amount of time to wait before requerying the status",
                val => TimeSpan.Parse(val), defaultRequeryDelay)
        ];

    public IEnumerable<Argument> GetCliArguments() => [];
}
