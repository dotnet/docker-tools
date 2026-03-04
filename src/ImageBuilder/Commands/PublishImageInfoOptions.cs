// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishImageInfoOptions : ImageInfoOptions, IGitOptionsHost
    {
        public GitOptions GitOptions { get; set; } = new GitOptions();
    }

    public class PublishImageInfoOptionsBuilder : ImageInfoOptionsBuilder
    {
        private readonly GitOptionsBuilder _gitOptionsBuilder = GitOptionsBuilder.BuildWithDefaults();

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._gitOptionsBuilder.GetCliOptions(),
            ];

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._gitOptionsBuilder.GetCliArguments()
            ];

        public override IEnumerable<ValidateSymbol<CommandResult>> GetValidators() =>
            [
                ..base.GetValidators(),
                .._gitOptionsBuilder.GetValidators()
            ];
    }
}
