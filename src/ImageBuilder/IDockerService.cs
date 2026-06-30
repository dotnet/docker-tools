// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IDockerService
    {
        Architecture Architecture { get; }

        void PullImage(string image, string? platform, bool isDryRun);

        void PushImage(string tag, bool isDryRun);

        void PushManifestList(string manifestListTag, bool isDryRun);

        void CreateTag(string image, string tag, bool isDryRun);

        void CreateManifestList(string manifestListTag, IEnumerable<string> images, bool isDryRun);

        /// <summary>
        /// Builds a Docker image.
        /// </summary>
        /// <param name="labels">
        /// Labels to apply to the image. Each entry translates to a <c>--label key=value</c> option on the
        /// <c>docker build</c> command.
        /// </param>
        string? BuildImage(
            string dockerfilePath,
            string buildContextPath,
            string platform,
            IEnumerable<string> tags,
            IDictionary<string, string?> buildArgs,
            IDictionary<string, string> labels,
            IEnumerable<string> dockerBuildOptions,
            bool isRetryEnabled,
            bool isDryRun);

        (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun);

        bool LocalImageExists(string tag, bool isDryRun);

        long GetImageSize(string image, bool isDryRun);

        DateTime GetCreatedDate(string image, bool isDryRun);
    }
}
