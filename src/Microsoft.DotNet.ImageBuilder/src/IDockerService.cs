// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IDockerService
    {
        Architecture Architecture { get; }

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

        bool LocalImageExists(string tag, bool isDryRun);

        long GetImageSize(string image, bool isDryRun);

        public string GetImageId(string image, bool isDryRun);

        public void DeleteImage(string imageId, bool isDryRun);
    }
}
