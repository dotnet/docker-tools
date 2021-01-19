// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrDocsOptions : ManifestOptions, IGitOptionsHost
    {
        public GitOptions GitOptions { get; } = new GitOptions();

        public string SourceRepoUrl { get; set; } = string.Empty;

        public PublishMcrDocsOptions() : base()
        {
        }
    }

    public class PublishMcrDocsOptionsBuilder : ManifestOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(GitOptions.GetCliOptions("Microsoft", "mcrdocs", "master", "teams"));

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(GitOptions.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(PublishMcrDocsOptions.SourceRepoUrl),
                            "Repo URL of the Dockerfile sources")
                    }
                );
    }
}
#nullable disable
