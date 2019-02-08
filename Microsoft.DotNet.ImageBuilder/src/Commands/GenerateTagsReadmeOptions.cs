// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateTagsReadmeOptions : Options
    {
        protected override string CommandHelp => "Generate the tags section of the readme";

        public string ReadmePath { get; set; }
        public bool SkipValidation { get; set; }
        public string SourceUrl { get; set; }
        public string Template { get; set; }
        public bool UpdateReadme { get; set; }

        public GenerateTagsReadmeOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            string readmePath = null;
            syntax.DefineOption("readme-path", ref readmePath, "Path of the readme to update (defaults to manifest setting)");
            ReadmePath = readmePath;

            bool skipValidation = false;
            syntax.DefineOption(
                "skip-validation", ref skipValidation, "Skip validating all documented tags are included in the readme");
            SkipValidation = skipValidation;

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
