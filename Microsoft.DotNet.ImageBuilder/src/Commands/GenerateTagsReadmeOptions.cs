// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateTagsReadmeOptions : ManifestOptions
    {
        protected override string CommandHelp => "Generates and updates the readme tag listing section";

        public string SourceRepoUrl { get; set; }

        public string SourceRepoBranch { get; set; }

        public GenerateTagsReadmeOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            string sourceRepoUrl = null;
            syntax.DefineParameter("source-repo", ref sourceRepoUrl, "Repo URL of the Dockerfile sources");
            SourceRepoUrl = sourceRepoUrl;

            string sourceRepoBranch = null;
            syntax.DefineParameter("source-branch", ref sourceRepoBranch, "Repo branch of the Dockerfile sources");
            SourceRepoBranch = sourceRepoBranch;
        }
    }
}
