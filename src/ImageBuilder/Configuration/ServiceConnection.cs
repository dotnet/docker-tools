// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public record ServiceConnection : IServiceConnection
{
    /// <inheritdoc/>
    public string Name { get; set; } = "";

    /// <inheritdoc/>
    public string TenantId { get; set; } = "";

    /// <inheritdoc/>
    public string ClientId { get; set; } = "";

    /// <inheritdoc/>
    public string Id { get; set; } = "";
}
