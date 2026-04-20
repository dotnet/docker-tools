// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class RegistryCredentialsOptions : IRegistryCredentialsHost
    {
        public IDictionary<string, RegistryCredentials> Credentials { get; set; } =
            new Dictionary<string, RegistryCredentials>();

        private static readonly Option<Dictionary<string, RegistryCredentials>> CredentialsOption =
            CliHelper.CreateDictionaryOption<RegistryCredentials>(
                "registry-creds",
                "Named credentials that map to a registry (<registry>=<username>;<password>)",
                val =>
                {
                    (string username, string password) = val.ParseKeyValuePair(';');
                    return new RegistryCredentials(username, password);
                });

        public IEnumerable<Option> GetCliOptions() => [CredentialsOption];

        public IEnumerable<Argument> GetCliArguments() => [];

        public void Bind(ParseResult result)
        {
            Credentials = result.GetValue(CredentialsOption) ?? new Dictionary<string, RegistryCredentials>();
        }
    }
}
