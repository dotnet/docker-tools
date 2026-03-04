// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.DotNet.ImageBuilder.Configuration;

public record ServiceConnection : IServiceConnection
{
    // The Name property is not used in ImageBuilder, but it is used in the Azure Pipeline templates.
    // See eng/pipelines/templates/stages/build-test-publish.yml for an example.
    // This code should serve as the source of truth for the service connection and publish configuration schemas,
    // so the property still needs to be included here.
    /// <inheritdoc/>
    public string Name { get; set; } = "";

    /// <inheritdoc/>
    public string TenantId { get; set; } = "";

    /// <inheritdoc/>
    public string ClientId { get; set; } = "";

    /// <inheritdoc/>
    public string Id { get; set; } = "";
}
