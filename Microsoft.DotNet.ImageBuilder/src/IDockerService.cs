// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IDockerService
    {
        void PullImage(string image, bool isDryRun);

        string GetImageDigest(string image, bool isDryRun);

        void PushImage(string tag, bool isDryRun);

        void BuildImage(
            string dockerfilePath,
            string buildContextPath,
            IEnumerable<string> tags,
            IDictionary<string, string> buildArgs,
            bool isRetryEnabled,
            bool isDryRun);
    }
}
