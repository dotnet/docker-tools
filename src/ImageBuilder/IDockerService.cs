// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IDockerService
    {
        Architecture Architecture { get; }

        void PullImage(string image, string? platform, bool isDryRun, CancellationToken cancellationToken);

        void PushImage(string tag, bool isDryRun, CancellationToken cancellationToken);

        void PushManifestList(string manifestListTag, bool isDryRun, CancellationToken cancellationToken);

        void CreateTag(string image, string tag, bool isDryRun, CancellationToken cancellationToken);

        void CreateManifestList(string manifestListTag, IEnumerable<string> images, bool isDryRun, CancellationToken cancellationToken);

        string? BuildImage(
            string dockerfilePath,
            string buildContextPath,
            string platform,
            IEnumerable<string> tags,
            IDictionary<string, string?> buildArgs,
            IReadOnlyDictionary<string, string> buildSecrets,
            BuildSecretMode buildSecretMode,
            IEnumerable<string> dockerBuildOptions,
            bool isRetryEnabled,
            bool isDryRun,
            CancellationToken cancellationToken);

        (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun, CancellationToken cancellationToken);

        bool LocalImageExists(string tag, bool isDryRun, CancellationToken cancellationToken);

        long GetImageSize(string image, bool isDryRun, CancellationToken cancellationToken);

        DateTime GetCreatedDate(string image, bool isDryRun, CancellationToken cancellationToken);
    }
}
