// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

public interface IAcrClientFactory
{
    IAcrClient Create(string acrName);

    /// <summary>
    /// Creates an ACR client using an explicit service connection instead of
    /// looking up the service connection from the publish configuration.
    /// </summary>
    IAcrClient Create(string acrName, IServiceConnection serviceConnection);
}
