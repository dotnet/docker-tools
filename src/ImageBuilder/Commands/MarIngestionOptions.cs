// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class MarIngestionOptions
{
    public TimeSpan WaitTimeout { get; set; }

    public TimeSpan RequeryDelay { get; set; }

    private Option<TimeSpan>? _timeoutOption;
    private Option<TimeSpan>? _requeryDelayOption;

    public IEnumerable<Option> GetCliOptions(TimeSpan defaultTimeout, TimeSpan defaultRequeryDelay)
    {
        _timeoutOption = new Option<TimeSpan>(CliHelper.FormatAlias("timeout"))
        {
            Description = "Maximum time to wait for ingestion",
            DefaultValueFactory = _ => defaultTimeout,
            CustomParser = argResult => TimeSpan.Parse(argResult.GetTokenValue())
        };

        _requeryDelayOption = new Option<TimeSpan>(CliHelper.FormatAlias("requery-delay"))
        {
            Description = "Amount of time to wait before requerying the status",
            DefaultValueFactory = _ => defaultRequeryDelay,
            CustomParser = argResult => TimeSpan.Parse(argResult.GetTokenValue())
        };

        return [_timeoutOption, _requeryDelayOption];
    }

    public IEnumerable<Argument> GetCliArguments() => [];

    public void Bind(ParseResult result)
    {
        ArgumentNullException.ThrowIfNull(_timeoutOption);
        ArgumentNullException.ThrowIfNull(_requeryDelayOption);
        WaitTimeout = result.GetValue(_timeoutOption);
        RequeryDelay = result.GetValue(_requeryDelayOption);
    }
}
