// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

public interface IGitHubFileRef : IGitHubBranchRef
{
    string Path { get; }
}
