// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;

namespace Microsoft.DotNet.ImageBuilder;

public interface IAcrClient
{
    Task DeleteRepositoryAsync(string name);

    IAsyncEnumerable<string> GetRepositoryNamesAsync();

    ContainerRepository GetRepository(string name);
}
