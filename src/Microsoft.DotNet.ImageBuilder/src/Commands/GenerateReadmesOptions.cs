// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateReadmesOptions : GenerateArtifactsOptions
    {
        protected override string CommandHelp =>
            "Generates the Readmes from the Cottle based templates (http://r3c.github.io/cottle/) and updates the tag listing section";

        public string SourceRepoUrl { get; set; }

        public string SourceRepoBranch { get; set; }

        public GenerateReadmesOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            string sourceRepoBranch = null;
            syntax.DefineOption("source-branch", ref sourceRepoBranch, "Repo branch of the Dockerfile sources (default is commit SHA)");
            SourceRepoBranch = sourceRepoBranch;
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string sourceRepoUrl = null;
            syntax.DefineParameter("source-repo", ref sourceRepoUrl, "Repo URL of the Dockerfile sources");
            SourceRepoUrl = sourceRepoUrl;
        }
    }
}
