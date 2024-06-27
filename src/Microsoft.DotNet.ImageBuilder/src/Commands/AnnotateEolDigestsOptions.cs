// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AnnotateEolDigestsOptions : Options
    {
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

        public string EolDigestsListPath { get; set; } = string.Empty;
        public string AcrName { get; set; } = string.Empty;
        public bool Force { get; set; } = false;
    }

    public class AnnotateEolDigestsOptionsBuilder : CliOptionsBuilder
    {
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_registryCredentialsOptionsBuilder.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        CreateOption<bool>("force", nameof(AnnotateEolDigestsOptions.Force),
                            "Annotate always, without checking if digest is already annotated for EOL"),
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_registryCredentialsOptionsBuilder.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(AnnotateEolDigestsOptions.EolDigestsListPath),
                            "EOL annotations digests list path"),
                        new Argument<string>(nameof(AnnotateEolDigestsOptions.AcrName),
                            "Azure registry name")
                    }
                );
    }
}
#nullable disable
