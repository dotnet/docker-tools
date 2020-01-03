// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ValidateImageSizeOptions : ImageSizeOptions
    {
        protected override string CommandHelp => "Validates the size of the images against a baseline";

        public bool CheckBaselineIntegrityOnly { get; set; }

        public ValidateImageSizeOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            bool checkBaselineIntegrityOnly = false;
            syntax.DefineOption("baseline-integrity-only", ref checkBaselineIntegrityOnly, "Only validate whether new or old images exist compared with baseline");
            CheckBaselineIntegrityOnly = checkBaselineIntegrityOnly;

            base.ParseCommandLine(syntax);
        }
    }
}
