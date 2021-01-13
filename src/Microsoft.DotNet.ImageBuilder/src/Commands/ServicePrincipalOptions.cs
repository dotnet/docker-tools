// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ServicePrincipalOptions
    {
        public string ClientId { get; set; } = string.Empty;

        public string Secret { get; set; } = string.Empty;

        public string Tenant { get; set; } = string.Empty;

        public static IEnumerable<Argument> GetCliArguments() =>
            new Argument[]
            {
                new Argument<string>(nameof(ClientId), "Client ID of service principal"),
                new Argument<string>(nameof(Secret), "Secret of service principal"),
                new Argument<string>(nameof(Tenant), "Tenant of service principal"),
            };
    }
}
#nullable disable
