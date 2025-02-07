// Licensed to the .NET Foundation under one or more agreements.
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
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

        public SubscriptionOptions SubscriptionOptions { get; set; } = new();

        public BaseImageOverrideOptions BaseImageOverrideOptions { get; set; } = new();
    }

    public class CopyBaseImagesOptionsBuilder : CopyImagesOptionsBuilder
    {
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();
        private readonly SubscriptionOptionsBuilder _subscriptionOptionsBuilder = new();
        private readonly BaseImageOverrideOptionsBuilder _baseImageOverrideOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_registryCredentialsOptionsBuilder.GetCliOptions())
                .Concat(_subscriptionOptionsBuilder.GetCliOptions())
                .Concat(_baseImageOverrideOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_registryCredentialsOptionsBuilder.GetCliArguments())
                .Concat(_subscriptionOptionsBuilder.GetCliArguments())
                .Concat(_baseImageOverrideOptionsBuilder.GetCliArguments());
    }
}
#nullable disable
