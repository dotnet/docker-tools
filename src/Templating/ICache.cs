// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.DockerTools.Templating;

public interface ICache<T>
{
    int Hits { get; }
    int Misses { get; }

    T GetOrAdd(string key);
}
