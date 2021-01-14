﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.VisualStudio.Services.Common;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
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
                CreateOption<string?>("azdo-repo", nameof(AzdoRepo), "Azure DevOps repo"),
                CreateOption<string?>("azdo-branch", nameof(AzdoBranch), "Azure DevOps branch (default: master)", "master"),
                CreateOption<string?>("azdo-path", nameof(AzdoPath), "Azure DevOps path"),
            };

        public (Uri BaseUrl, VssCredentials Credentials) GetConnectionDetails() =>
            (new Uri($"https://dev.azure.com/{Organization}"), new VssBasicCredential(string.Empty, AccessToken));
    }
}
#nullable disable
