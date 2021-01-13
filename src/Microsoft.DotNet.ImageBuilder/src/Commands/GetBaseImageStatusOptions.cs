// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetBaseImageStatusOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public bool ContinuousMode { get; set; }

        public TimeSpan ContinuousModeDelay { get; set; }
    }

    public class GetBaseImageStatusSymbolsBuilder : ManifestSymbolsBuilder
    {
        private static readonly TimeSpan ContinuousModeDelayDefault = TimeSpan.FromSeconds(10);

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(ManifestFilterOptions.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        new Option<bool>("--continuous", "Runs the status check continuously")
                        {
                            Name = nameof(GetBaseImageStatusOptions.ContinuousMode)
                        },
                        new Option<TimeSpan>("--continuous-delay",
                            description: $"Delay before running next status check (default {ContinuousModeDelayDefault.TotalSeconds} secs)",
                            parseArgument: resultArg => TimeSpan.FromSeconds(int.Parse(resultArg.Tokens.First().Value)))
                        {
                            Argument = new Argument<TimeSpan>(() => ContinuousModeDelayDefault),
                            Name = nameof(GetBaseImageStatusOptions.ContinuousModeDelay)
                        }
                    });
    }
}
#nullable disable
