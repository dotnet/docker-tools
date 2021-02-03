// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class DockerCredsOptions
    {
        public string? DockerUsername { get; set; }
        public string? DockerPassword { get; set; }
        public bool AllowAnonymousAccess { get; set; }
    }

    public class DockerCredsOptionsBuilder
    {
        public IEnumerable<Option> GetCliOptions() =>
            new Option[]
            {
                CreateOption<string>("docker-user", nameof(DockerCredsOptions.DockerUsername),
                    "Username of the Docker Hub account"),
                CreateOption<string>("docker-password", nameof(DockerCredsOptions.DockerPassword),
                    "Password of the Docker Hub account"),
                CreateOption<bool>("allow-anon-docker-access", nameof(DockerCredsOptions.AllowAnonymousAccess),
                    "Explicitly allows anonymous access to the Docker Hub registry")
            };

        public IEnumerable<Argument> GetCliArguments() => Enumerable.Empty<Argument>();
    }
}
#nullable disable
