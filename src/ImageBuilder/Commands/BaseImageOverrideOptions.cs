// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.RegularExpressions;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands;

/// <summary>
/// Defines options that allow the caller to configure whether and how base image tags defined in a Dockerfile
/// are to be overriden.
/// </summary>
/// This allows for images to be sourced from a different location than described in the Dockerfile.
/// For example, the Build command implements this by pulling an image from the overriden location, retagging it with the
/// tag used in the Dockerfile, and continue with the rest of the build.<remarks>
/// </remarks>
#nullable enable
public class BaseImageOverrideOptions
{
    public const string BaseOverrideRegexName = "base-override-regex";
    public const string BaseOverrideSubName = "base-override-sub";

    public string? RegexPattern { get; set; }

    public string? Substitution { get; set; }

    public void Validate()
    {
        if (RegexPattern is null != Substitution is null)
        {
            throw new InvalidOperationException(
                $"The '{BaseOverrideRegexName}' and '{BaseOverrideSubName}' options must both be set.");
        }
    }

    public string ApplyBaseImageOverride(string imageName)
    {
        if (RegexPattern is not null && Substitution is not null)
        {
            return Regex.Replace(imageName, RegexPattern, Substitution);
        }

        return imageName;
    }
}

public class BaseImageOverrideOptionsBuilder
{
    public IEnumerable<Option> GetCliOptions() =>
        new Option[]
        {
            CreateOption<string?>(BaseImageOverrideOptions.BaseOverrideRegexName, nameof(BaseImageOverrideOptions.RegexPattern),
                    $"Regular expression identifying base image tags to apply string substitution to (requires {BaseImageOverrideOptions.BaseOverrideSubName} to be set)"),
                CreateOption<string?>(BaseImageOverrideOptions.BaseOverrideSubName, nameof(BaseImageOverrideOptions.Substitution),
                    $"Regular expression substitution that overrides a matching base image tag (requires {BaseImageOverrideOptions.BaseOverrideRegexName} to be set)")
        };

    public IEnumerable<Argument> GetCliArguments() => Enumerable.Empty<Argument>();
}

#nullable disable
