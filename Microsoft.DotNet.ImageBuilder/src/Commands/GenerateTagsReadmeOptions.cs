// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateTagsReadmeOptions : Options
    {
        protected override string CommandHelp => "Generate the tags section of the readme";
        protected override string CommandName => "generateTagsReadme";

        public string SourceUrl { get; set; }
        public string Template { get; set; }
        public bool UpdateReadme { get; set; }

        public GenerateTagsReadmeOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            bool updateReadme = false;
            syntax.DefineOption("update-readme", ref updateReadme, "Update the readme file");
            UpdateReadme = updateReadme;

            string template = null;
            syntax.DefineOption("template", ref template, "Path to a custom template file");
            Template = template;

            string sourceUrl = null;
            syntax.DefineParameter("source-url", ref sourceUrl, "Base URL of the Dockerfile sources");
            SourceUrl = sourceUrl;
        }
    }
}
