// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder;

public interface IAcrContentClientFactory
{
    IAcrContentClient Create(Acr acr, string repositoryName);
}
