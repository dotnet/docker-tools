// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class AzdoOptions
{
    public string AccessToken { get; set; } = string.Empty;

    public string Organization { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;

    public string? AzdoRepo { get; set; }

    public string? AzdoBranch { get; set; }

    public string? AzdoPath { get; set; }

    private static readonly Argument<string> AccessTokenArgument = new(nameof(AccessToken))
    {
        Description = "Azure DevOps PAT"
    };

    private static readonly Argument<string> OrganizationArgument = new(nameof(Organization))
    {
        Description = "Azure DevOps organization"
    };

    private static readonly Argument<string> ProjectArgument = new(nameof(Project))
    {
        Description = "Azure DevOps project"
    };

    private static readonly Option<string?> AzdoRepoOption = new(CliHelper.FormatAlias("azdo-repo"))
    {
        Description = "Azure DevOps repo"
    };

    private static readonly Option<string?> AzdoBranchOption = new(CliHelper.FormatAlias("azdo-branch"))
    {
        Description = "Azure DevOps branch (default: main)",
        DefaultValueFactory = _ => "main"
    };

    private static readonly Option<string?> AzdoPathOption = new(CliHelper.FormatAlias("azdo-path"))
    {
        Description = "Azure DevOps path"
    };

    public IEnumerable<Argument> GetCliArguments() =>
        [AccessTokenArgument, OrganizationArgument, ProjectArgument];

    public IEnumerable<Option> GetCliOptions() =>
        [AzdoRepoOption, AzdoBranchOption, AzdoPathOption];

    public void Bind(ParseResult result)
    {
        AccessToken = result.GetValue(AccessTokenArgument) ?? string.Empty;
        Organization = result.GetValue(OrganizationArgument) ?? string.Empty;
        Project = result.GetValue(ProjectArgument) ?? string.Empty;
        AzdoRepo = result.GetValue(AzdoRepoOption);
        AzdoBranch = result.GetValue(AzdoBranchOption);
        AzdoPath = result.GetValue(AzdoPathOption);
    }

    public (Uri BaseUrl, VssCredentials Credentials) GetConnectionDetails() =>
        (new Uri($"https://dev.azure.com/{Organization}"), new VssBasicCredential(string.Empty, AccessToken));
}
