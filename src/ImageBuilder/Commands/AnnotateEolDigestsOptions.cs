// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AnnotateEolDigestsOptions : Options
    {
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

        public string EolDigestsListPath { get; set; } = string.Empty;
        public string AcrName { get; set; } = string.Empty;
        public string RepoPrefix { get; set; } = string.Empty;
        public string AnnotationDigestsOutputPath { get; set; } = string.Empty;
    }

    public class AnnotateEolDigestsOptionsBuilder : CliOptionsBuilder
    {
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._registryCredentialsOptionsBuilder.GetCliOptions()
            ];

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._registryCredentialsOptionsBuilder.GetCliArguments(),
                new Argument<string>(nameof(AnnotateEolDigestsOptions.EolDigestsListPath),
                    "EOL annotations digests list path"),
                new Argument<string>(nameof(AnnotateEolDigestsOptions.AcrName),
                    "Azure registry name"),
                new Argument<string>(nameof(AnnotateEolDigestsOptions.RepoPrefix),
                    "Publish prefix of the repo names"),
                new Argument<string>(nameof(AnnotateEolDigestsOptions.AnnotationDigestsOutputPath),
                    "Output path of file containing the list of annotation digests that were created"),
            ];
    }
}
#nullable disable
