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
    public abstract class DockerRegistryOptions : ManifestOptions
    {
        public string? Password { get; set; }
        public string? Username { get; set; }
    }

    public abstract class DockerRegistryOptionsBuilder : ManifestOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions()
        {
            Option<string?>? passwordOption = null;
            Option<string?>? usernameOption = null;
            return base.GetCliOptions().Concat(
                new Option[]
                {
                    (passwordOption = CreateOption<string?>("password", nameof(DockerRegistryOptions.Password),
                        "Password for the Docker Registry the images are pushed to",
                        resultArg =>
                        {
                            string password = resultArg.GetTokenValue();
                            string? username = resultArg.FindResultFor(
                                usernameOption ?? throw new InvalidOperationException("username option not set yet"))?.GetTokenValue();
                            ValidateUsernameAndPassword(username, password);
                            return password;
                        })),
                    (usernameOption = CreateOption<string?>("username", nameof(DockerRegistryOptions.Username),
                        "Username for the Docker Registry the images are pushed to",
                        resultArg =>
                        {
                            string username = resultArg.GetTokenValue();
                            string? password = resultArg.FindResultFor(
                                passwordOption ?? throw new InvalidOperationException("password option not set yet"))?.GetTokenValue();
                            ValidateUsernameAndPassword(username, password);
                            return username;
                        }))
                });
        }

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
