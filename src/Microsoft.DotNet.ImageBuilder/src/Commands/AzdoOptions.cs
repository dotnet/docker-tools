// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.VisualStudio.Services.Common;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AzdoOptions
    {
        public string AccessToken { get; set; } = string.Empty;

        public string Organization { get; set; } = string.Empty;

        public string Project { get; set; } = string.Empty;

        public string? Repo { get; set; }

        public string? Branch { get; set; }

        public string? Path { get; set; }

        public static IEnumerable<Argument> GetCliArguments() =>
            new Argument[]
            {
                new Argument<string>(nameof(AccessToken), "Azure DevOps PAT"),
                new Argument<string>(nameof(Organization), "Azure DevOps organization"),
                new Argument<string>(nameof(Project), "Azure DevOps project")
            };

        public static IEnumerable<Option> GetCliOptions() =>
            new Option[]
            {
                new Option<string?>("--azdo-repo", "Azure DevOps repo"),
                new Option<string?>("--azdo-branch", () => "master", "Azure DevOps branch (default: master)"),
                new Option<string?>("--azdo-path", "Azure DevOps path"),
            };

        public (Uri BaseUrl, VssCredentials Credentials) GetConnectionDetails() =>
            (new Uri($"https://dev.azure.com/{Organization}"), new VssBasicCredential(string.Empty, AccessToken));
    }
}
#nullable disable
