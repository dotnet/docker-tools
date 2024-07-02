// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateEolAnnotationDataOptions : Options
    {
        public string EolDigestsListPath { get; set; } = string.Empty;
        public string? OldImageInfoPath { get; set; }
        public string? NewImageInfoPath { get; set; }
        public bool AnnotateEolProducts { get; set; } = false;
        public string? RepoPrefix { get; set; }
    }

    public class GenerateEolAnnotationDataOptionsBuilder : CliOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption<string>("old-image-info-path", nameof(GenerateEolAnnotationDataOptions.OldImageInfoPath),
                            "Old image-info file"),
                        CreateOption<string>("new-image-info-path", nameof(GenerateEolAnnotationDataOptions.NewImageInfoPath),
                            "New image-info file"),
                        CreateOption<bool>("annotate-eol-products", nameof(GenerateEolAnnotationDataOptions.AnnotateEolProducts),
                            "Annotate images of EOL products"),
                        CreateOption<string>("repo-prefix", nameof(GenerateEolAnnotationDataOptions.RepoPrefix),
                            "Prefix to add to the repo names specified in the manifest"),
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(AnnotateEolDigestsOptions.EolDigestsListPath),
                            "Eol annotations digests list path")
                    }
                );
    }
}
#nullable disable
