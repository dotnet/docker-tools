// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record AcrConfiguration
{
    public static string ConfigurationKey => nameof(AcrConfiguration);

    public Acr? Registry { get; set; } = null;
    public string? ResourceGroup { get; set; } = null;
    public string? Subscription { get; set; } = null;
    public string? RepoPrefix { get; set; } = null;
    public ServiceConnectionOptions? ServiceConnection { get; set; } = null;
}
