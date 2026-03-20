// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.VisualStudio.Services.Common;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AzdoOptions
    {
        public string AccessToken { get; set; } = string.Empty;

        public string Organization { get; set; } = string.Empty;

        public string Project { get; set; } = string.Empty;

        public string? AzdoRepo { get; set; }

        public string? AzdoBranch { get; set; }

        public string? AzdoPath { get; set; }

        public (Uri BaseUrl, VssCredentials Credentials) GetConnectionDetails() =>
            (new Uri($"https://dev.azure.com/{Organization}"), new VssBasicCredential(string.Empty, AccessToken));
    }

    public class AzdoOptionsBuilder
    {
        public IEnumerable<Argument> GetCliArguments() =>
            new Argument[]
            {
                new Argument<string>(nameof(AzdoOptions.AccessToken), "Azure DevOps PAT"),
                new Argument<string>(nameof(AzdoOptions.Organization), "Azure DevOps organization"),
                new Argument<string>(nameof(AzdoOptions.Project), "Azure DevOps project")
            };

        public IEnumerable<Option> GetCliOptions() =>
            new Option[]
            {
                CreateOption<string?>("azdo-repo", nameof(AzdoOptions.AzdoRepo), "Azure DevOps repo"),
                CreateOption<string?>("azdo-branch", nameof(AzdoOptions.AzdoBranch), "Azure DevOps branch (default: main)", "main"),
                CreateOption<string?>("azdo-path", nameof(AzdoOptions.AzdoPath), "Azure DevOps path"),
            };
    }
}
