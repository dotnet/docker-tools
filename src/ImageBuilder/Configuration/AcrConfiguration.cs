// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record AcrConfiguration
{
    // TODO: This was temporarily renamed to Server to match existing YAML configuration.
    // See publish-config-prod.yml and publish-config-prod.yml.
    // It should be renamed back to Repo once the YAML files are updated.
    public string? Server { get; set; } = null;
    public string? ResourceGroup { get; set; } = null;
    public string? Subscription { get; set; } = null;
    public string? RepoPrefix { get; set; } = null;
    public ServiceConnection? ServiceConnection { get; set; } = null;
}
