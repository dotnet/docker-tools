// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateTagsReadmeOptions : Options
    {
        protected override string CommandHelp => "Generates and updates the readme tag listing section";

        public string SourceUrl { get; set; }

        public GenerateTagsReadmeOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            string sourceUrl = null;
            syntax.DefineParameter("source-url", ref sourceUrl, "Base URL of the Dockerfile sources");
            SourceUrl = sourceUrl;
        }
    }
}
