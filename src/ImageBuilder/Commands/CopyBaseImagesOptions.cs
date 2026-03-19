// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyBaseImagesOptions : CopyImagesOptions
    {
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

        public SubscriptionOptions SubscriptionOptions { get; set; } = new();

        public BaseImageOverrideOptions BaseImageOverrideOptions { get; set; } = new();

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
            ..SubscriptionOptions.GetCliOptions(),
            ..BaseImageOverrideOptions.GetCliOptions(),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..CredentialsOptions.GetCliArguments(),
            ..SubscriptionOptions.GetCliArguments(),
            ..BaseImageOverrideOptions.GetCliArguments(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            CredentialsOptions.Bind(result);
            SubscriptionOptions.Bind(result);
            BaseImageOverrideOptions.Bind(result);
        }
    }
}
