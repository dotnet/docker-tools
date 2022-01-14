// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public interface IDockerService
    {
        Architecture Architecture { get; }

        void PullImage(string image, bool isDryRun);

        string? GetImageDigest(string image, bool isDryRun);

        IEnumerable<string> GetImageManifestLayers(string image, bool isDryRun);

        void PushImage(string tag, bool isDryRun);

        void CreateTag(string image, string tag, bool isDryRun);

        string? BuildImage(
            string dockerfilePath,
            string buildContextPath,
            string platform,
            IEnumerable<string> tags,
            IDictionary<string, string?> buildArgs,
            bool isRetryEnabled,
            bool isDryRun);

        (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun);

        bool LocalImageExists(string tag, bool isDryRun);

        long GetImageSize(string image, bool isDryRun);

        DateTime GetCreatedDate(string image, bool isDryRun);
    }
}
#nullable disable
