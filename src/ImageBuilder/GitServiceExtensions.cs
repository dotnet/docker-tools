#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class GitServiceExtensions
    {
        public static string GetDockerfileCommitUrl(
            this IGitService gitService,
            PlatformInfo platform,
            string sourceRepoUrl,
            string sourceBranch = null)
        {
            string branchOrShaPathSegment = sourceBranch ??
                gitService.GetCommitSha(platform.DockerfilePath, useFullHash: true);

            string dockerfileRelativePath = PathHelper.NormalizePath(platform.DockerfilePathRelativeToManifest);
            return $"{sourceRepoUrl}/blob/{branchOrShaPathSegment}/{dockerfileRelativePath}";
        }
    }
}
