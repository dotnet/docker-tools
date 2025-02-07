﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.ImageBuilder.Models.McrStatus;

namespace Microsoft.DotNet.DockerTools.ImageBuilder
{
    public interface IMcrStatusClient
    {
        Task<ImageResult> GetImageResultAsync(string imageDigest);
        Task<ImageResultDetailed> GetImageResultDetailedAsync(string imageDigest, string onboardingRequestId);
        Task<CommitResult> GetCommitResultAsync(string commitDigest);
        Task<CommitResultDetailed> GetCommitResultDetailedAsync(string commitDigest, string onboardingRequestId);
    }
}
