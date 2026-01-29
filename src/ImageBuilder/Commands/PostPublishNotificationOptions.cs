// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PostPublishNotificationOptions : ManifestOptions
    {
        public IList<string> TaskNames { get; set; } = new List<string>();
        public string SourceRepo { get; set; } = string.Empty;
        public string SourceBranch { get; set; } = string.Empty;
        public string ImageInfoPath { get; set; } = string.Empty;
        public int BuildId { get; set; }
        public GitOptions GitOptions { get; set; } = new();
        public AzdoOptions AzdoOptions { get; set; } = new();
    }

    public class PostPublishNotificationOptionsBuilder : ManifestOptionsBuilder
    {
        private readonly AzdoOptionsBuilder _azdoOptionsBuilder = new();
        private readonly GitOptionsBuilder _gitOptionsBuilder =
            GitOptionsBuilder.Build()
                .WithGitHubAuth(
                    isRequired: true,
                    description: "Auth token to use to connect to GitHub for posting notifications")
                .WithOwner(isRequired: true, description: "Owner of the GitHub repo to post notifications to")
                .WithRepo(isRequired: true, description: "Name of the GitHub repo to post notifications to");

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(new Option[]
                {
                    CreateMultiOption<string>("task", nameof(PostPublishNotificationOptions.TaskNames),
                        "Name of a build task to report the result of")
                })
                .Concat(_azdoOptionsBuilder.GetCliOptions())
                .Concat(_gitOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(new Argument[]
                {
                    new Argument(nameof(PostPublishNotificationOptions.SourceRepo))
                    {
                        Description = "Name of the repo that is the source of the publish"
                    },
                    new Argument(nameof(PostPublishNotificationOptions.SourceBranch))
                    {
                        Description = "Name of the repo branch that is the source of the publish"
                    },
                    new Argument(nameof(PostPublishNotificationOptions.ImageInfoPath))
                    {
                        Description = "Path to image info file"
                    },
                    new Argument(nameof(PostPublishNotificationOptions.BuildId))
                    {
                        Description = "ID of the build that executed the publish"
                    }
                })
                .Concat(_azdoOptionsBuilder.GetCliArguments())
                .Concat(_gitOptionsBuilder.GetCliArguments());

        public override IEnumerable<ValidateSymbol<CommandResult>> GetValidators() =>
            [
                ..base.GetValidators(),
                .._gitOptionsBuilder.GetValidators()
            ];
    }
}
