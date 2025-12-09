// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record RegistryConfiguration
{
    public string? Server { get; set; } = null;
    public string? ResourceGroup { get; set; } = null;
    public string? Subscription { get; set; } = null;
    public string? RepoPrefix { get; set; } = null;
    public ServiceConnection? ServiceConnection { get; set; } = null;
}
