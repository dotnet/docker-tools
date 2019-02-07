// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ValidateImageSizeOptions : Options, IManifestFilterOptions
    {
        protected override string CommandHelp => "Validates the size of the images against a baseline";
        protected override string CommandName => "validateImageSize";

        public int AllowedVariance { get; set; }
        public string Architecture { get; set; }
        public string BaselinePath { get; set; }
        public bool IsPullEnabled { get; set; }
        public string OsType { get; set; }
        public string OsVersion { get; set; }
        public IEnumerable<string> Paths { get; set; }
        public bool UpdateBaseline { get; set; }

        public ValidateImageSizeOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            DefineManifestFilterOptions(syntax, this);

            int allowedVariance = 5;
            syntax.DefineOption("variance", ref allowedVariance, $"Allowed percent variance in size (default is `{allowedVariance}`");
            AllowedVariance = allowedVariance;

            bool isPullEnabled = false;
            syntax.DefineOption("pull", ref isPullEnabled, "Pull the images vs using local images");
            IsPullEnabled = isPullEnabled;

            bool updateBaseline = false;
            syntax.DefineOption("update", ref updateBaseline, "Update the baseline file (default is false)");
            UpdateBaseline = updateBaseline;

            string baselinePath = null;
            syntax.DefineParameter("baseline", ref baselinePath, "Path to the baseline file");
            BaselinePath = baselinePath;
        }
    }
}
