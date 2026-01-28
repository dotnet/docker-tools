// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetBaseImageStatusOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public bool ContinuousMode { get; set; }

        public TimeSpan ContinuousModeDelay { get; set; }
    }

    public class GetBaseImageStatusOptionsBuilder : ManifestOptionsBuilder
    {
        private static readonly TimeSpan ContinuousModeDelayDefault = TimeSpan.FromSeconds(10);

        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder =
            new ManifestFilterOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        CreateOption<bool>("continuous", nameof(GetBaseImageStatusOptions.ContinuousMode),
                            "Runs the status check continuously"),
                        CreateOption("continuous-delay", nameof(GetBaseImageStatusOptions.ContinuousModeDelay),
                            $"Delay before running next status check (default {ContinuousModeDelayDefault.TotalSeconds} secs)",
                            val => TimeSpan.FromSeconds(int.Parse(val)), ContinuousModeDelayDefault)
                    });

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments());
    }
}
