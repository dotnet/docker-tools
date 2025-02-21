﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IContainerRegistryContentClientFactory
{
    IContainerRegistryContentClient Create(string acrName, string repositoryName, TokenCredential credential);
}
