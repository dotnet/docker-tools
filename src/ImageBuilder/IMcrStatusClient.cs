// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IMcrStatusClient
    {
        Task<ImageResult> GetImageResultAsync(string imageDigest, CancellationToken cancellationToken);
        Task<ImageResultDetailed> GetImageResultDetailedAsync(string imageDigest, string onboardingRequestId, CancellationToken cancellationToken);
        Task<CommitResult> GetCommitResultAsync(string commitDigest, CancellationToken cancellationToken);
        Task<CommitResultDetailed> GetCommitResultDetailedAsync(string commitDigest, string onboardingRequestId, CancellationToken cancellationToken);
    }
}
