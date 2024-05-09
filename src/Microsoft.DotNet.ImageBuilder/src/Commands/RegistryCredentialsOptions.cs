// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class RegistryCredentialsOptions : IRegistryCredentialsHost
    {
        public IDictionary<string, RegistryCredentials> Credentials { get; set; } =
            new Dictionary<string, RegistryCredentials>();
        public string? Tenant { get; set; }
    }

    public class RegistryCredentialsOptionsBuilder
    {
        public IEnumerable<Option> GetCliOptions() =>
            [
                CreateDictionaryOption("registry-creds", nameof(RegistryCredentialsOptions.Credentials),
                    "Named credentials that map to a registry (<registry>=<username>;<password>)",
                    val =>
                        {
                            (string username, string password) = val.ParseKeyValuePair(';');
                            return new RegistryCredentials(username, password);
                        }),
                CreateOption<string?>("tenant", nameof(RegistryCredentialsOptions.Tenant),
                    "Tenant containing the ACR to authenticate to"),
            ];

        public IEnumerable<Argument> GetCliArguments() => [];
    }
}
#nullable disable
