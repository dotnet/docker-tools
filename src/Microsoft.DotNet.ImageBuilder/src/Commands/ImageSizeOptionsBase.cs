// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ImageSizeOptionsBase : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions => new ManifestFilterOptions();

        public int AllowedVariance { get; set; }
        public string BaselinePath { get; set; }
        public bool IsPullEnabled { get; set; }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            FilterOptions.ParseCommandLine(syntax);

            int allowedVariance = 5;
            syntax.DefineOption("variance", ref allowedVariance, $"Allowed percent variance in size (default is `{allowedVariance}`");
            AllowedVariance = allowedVariance;

            bool isPullEnabled = false;
            syntax.DefineOption("pull", ref isPullEnabled, "Pull the images vs using local images");
            IsPullEnabled = isPullEnabled;

            string baselinePath = null;
            syntax.DefineParameter("baseline", ref baselinePath, "Path to the baseline file");
            BaselinePath = baselinePath;
        }
    }
}
