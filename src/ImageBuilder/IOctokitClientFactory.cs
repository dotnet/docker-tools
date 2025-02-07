// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public interface IOctokitClientFactory
    {
        IBlobsClient CreateBlobsClient(IApiConnection connection);
        ITreesClient CreateTreesClient(IApiConnection connection);
    }
}
#nullable disable
