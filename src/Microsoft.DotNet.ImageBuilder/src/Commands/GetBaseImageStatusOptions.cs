// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetBaseImageStatusOptions : ManifestOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Displays the status of the referenced external base images";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public bool ContinuousMode { get; set; }

        public TimeSpan ContinuousModeDelay { get; set; }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);

            bool continuousMode = false;
            syntax.DefineOption("continuous", ref continuousMode, "Runs the status check continuously");
            ContinuousMode = continuousMode;

            const int ContinuousModeDelayDefault = 10;
            int continuousModeDelay = ContinuousModeDelayDefault;
            syntax.DefineOption("continuous-delay", ref continuousModeDelay,
                $"Delay before running next status check (default {ContinuousModeDelayDefault} secs)");
            ContinuousModeDelay = TimeSpan.FromSeconds(continuousModeDelay);
        }
    }
}
