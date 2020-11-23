// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AzdoOptions
    {
        public string AccessToken { get; set; }

        public string Organization { get; set; }

        public string Project { get; set; }

        public string Repo { get; set; }

        public string Branch { get; set; }

        public string Path { get; set; }

        public void DefineParameters(ArgumentSyntax syntax)
        {
            string accessToken = null;
            syntax.DefineParameter(
                "azdo-pat",
                ref accessToken,
                "Azure DevOps PAT");
            AccessToken = accessToken;

            string organization = null;
            syntax.DefineParameter(
                "azdo-org",
                ref organization,
                "Azure DevOps organization");
            Organization = organization;

            string project = null;
            syntax.DefineParameter(
                "azdo-project",
                ref project,
                "Azure DevOps project");
            Project = project;
        }

        public void DefineOptions(ArgumentSyntax syntax)
        {
            string repo = null;
            syntax.DefineOption(
                "azdo-repo",
                ref repo,
                "Azure DevOps repo");
            Repo = repo;

            string branch = "master";
            syntax.DefineOption(
                "azdo-branch",
                ref branch,
                "Azure DevOps branch (default: master)");
            Branch = branch;

            string path = null;
            syntax.DefineOption(
                "azdo-path",
                ref path,
                "Azure DevOps path");
            Path = path;
        }

        public (Uri BaseUrl, VssCredentials Credentials) GetConnectionDetails() =>
            (new Uri($"https://dev.azure.com/{Organization}"), new VssBasicCredential(string.Empty, AccessToken));
    }
}
