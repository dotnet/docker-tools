﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IContainerRegistryClient
{
    Task DeleteRepositoryAsync(string name);

    IAsyncEnumerable<string> GetRepositoryNames();

    ContainerRepository GetRepository(string name);
}
