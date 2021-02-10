// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class RegistryCredentialsOptions
    {
        public IDictionary<string, RegistryCredentials> Credentials { get; set; } =
            new Dictionary<string, RegistryCredentials>();
    }

    public class RegistryCredentialsOptionsBuilder
    {
        public IEnumerable<Option> GetCliOptions() =>
            new Option[]
            {
                CreateDictionaryOption("registry-creds", nameof(RegistryCredentialsOptions.Credentials),
                    "Named credentials that map to a registry (<registry>=<username>;<password>)",
                    val =>
                        {
                            (string username, string password) = val.ParseKeyValuePair(';');
                            return new RegistryCredentials(username, password);
                        })
            };

        public IEnumerable<Argument> GetCliArguments() => Enumerable.Empty<Argument>();
    }

    public class RegistryCredentials
    {
        public RegistryCredentials(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }
        public string Password { get; }
    }
}
#nullable disable
