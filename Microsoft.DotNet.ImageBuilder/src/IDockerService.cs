// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IDockerService
    {
        void PullImage(string image, bool isDryRun);

        string GetImageDigest(string image, bool isDryRun);
    }
}
