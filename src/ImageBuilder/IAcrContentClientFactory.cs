// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder;

public interface IAcrContentClientFactory
{
    IAcrContentClient Create(Acr acr, string repositoryName);

    /// <summary>
    /// Creates an ACR content client using an explicit service connection instead of
    /// looking up the service connection from the publish configuration.
    /// </summary>
    IAcrContentClient Create(Acr acr, string repositoryName, IServiceConnection serviceConnection);
}
