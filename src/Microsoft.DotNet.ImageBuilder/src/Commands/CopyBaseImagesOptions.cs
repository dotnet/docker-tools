﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyBaseImagesOptions : CopyImagesOptions
    {
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new RegistryCredentialsOptions();
    }

    public class CopyBaseImagesOptionsBuilder : CopyImagesOptionsBuilder
    {
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder =
            new RegistryCredentialsOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions().Concat(_registryCredentialsOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments().Concat(_registryCredentialsOptionsBuilder.GetCliArguments());
    }
}
#nullable disable
