// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class DockerRegistryOptions : ManifestOptions
    {
        public string? Password { get; set; }
        public string? Username { get; set; }
    }

    public abstract class DockerRegistrySymbolsBuilder : ManifestSymbolsBuilder
    {
        public override IEnumerable<Option> GetCliOptions()
        {
            Option<string?>? passwordOption = null;
            Option<string?>? usernameOption = null;
            return base.GetCliOptions().Concat(
                new Option[]
                {
                    (passwordOption = new Option<string?>("--password", description: "Password for the Docker Registry the images are pushed to",
                        parseArgument: resultArg =>
                        {
                            string? password = GetTokenValue(resultArg);
                            ValidateUsernameAndPassword(
                                GetTokenValue(resultArg.FindResultFor(usernameOption ?? throw new InvalidOperationException("username option not set yet"))),
                                password);
                            return password;
                        })
                    {
                        Name = nameof(DockerRegistryOptions.Password)
                    }),
                    new Option<string?>("--username", description: "Username for the Docker Registry the images are pushed to",
                        parseArgument: resultArg =>
                            {
                                string? username = GetTokenValue(resultArg);
                                ValidateUsernameAndPassword(
                                    username,
                                    GetTokenValue(resultArg.FindResultFor(passwordOption ?? throw new InvalidOperationException("password option not set yet"))));
                                return username;
                            })
                    {
                        Name = nameof(DockerRegistryOptions.Username)
                    },
                });
        }

        private static string? GetTokenValue(SymbolResult? symbolResult) => symbolResult?.Tokens.First().Value;

        private static void ValidateUsernameAndPassword(string? username, string? password)
        {
            if (!string.IsNullOrEmpty(username) ^ !string.IsNullOrEmpty(password))
            {
                Logger.WriteError($"error: `username` and `password` must both be specified.");
                Environment.Exit(1);
            }
        }
    }
}
#nullable disable
