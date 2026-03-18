// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetBaseImageStatusOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public bool ContinuousMode { get; set; }

        public TimeSpan ContinuousModeDelay { get; set; }

        private static readonly TimeSpan ContinuousModeDelayDefault = TimeSpan.FromSeconds(10);

        private static readonly Option<bool> ContinuousModeOption = new(CliHelper.FormatAlias("continuous"))
        {
            Description = "Runs the status check continuously"
        };

        private static readonly Option<TimeSpan> ContinuousModeDelayOption = new(CliHelper.FormatAlias("continuous-delay"))
        {
            Description = $"Delay before running next status check (default {ContinuousModeDelayDefault.TotalSeconds} secs)",
            DefaultValueFactory = _ => ContinuousModeDelayDefault,
            CustomParser = argResult => TimeSpan.FromSeconds(int.Parse(argResult.GetTokenValue()))
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            ContinuousModeOption,
            ContinuousModeDelayOption,
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..FilterOptions.GetCliArguments(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            FilterOptions.Bind(result);
            ContinuousMode = result.GetValue(ContinuousModeOption);
            ContinuousModeDelay = result.GetValue(ContinuousModeDelayOption);
        }
    }
}
