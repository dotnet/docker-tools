// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;

namespace Microsoft.DotNet.ImageBuilder;

public class LifecycleMetadataService(IOrasService orasService, ILogger<LifecycleMetadataService> logger)
    : ILifecycleMetadataService
{
    public async Task<LifecycleArtifact?> GetLatestLifecycleArtifactAsync(
        string digest,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ReferrerInfo> referrers =
            await orasService.GetReferrersAsync(digest, cancellationToken, isDryRun: false);

        ReferrerInfo? lifecycleReferrer = referrers
            .Where(r => r.ArtifactType == OciArtifactType.Lifecycle && (includeInternal || !r.IsInternal))
            .OrderByDescending(r => r.Created)
            .FirstOrDefault();

        if (lifecycleReferrer is null)
        {
            return null;
        }

        return new LifecycleArtifact(lifecycleReferrer);
    }

    public async Task<LifecycleArtifact?> AnnotateEolDigestAsync(
        string digest,
        DateOnly date,
        bool markAsInternal,
        CancellationToken cancellationToken)
    {
        try
        {
            // Set the creation time explicitly so the returned artifact matches what was pushed.
            Dictionary<string, string> annotations = new()
            {
                [LifecycleAnnotations.EndOfLife] = date.ToString(LifecycleAnnotations.EndOfLifeDateFormat),
                [OciAnnotations.ImageCreated] = DateTimeOffset.UtcNow.ToString("o")
            };

            if (markAsInternal)
            {
                annotations[ImageBuilderAnnotations.Internal] = "true";
            }

            string artifactDigest = await orasService.AttachArtifactAsync(
                digest,
                OciArtifactType.Lifecycle,
                annotations,
                cancellationToken);

            // Construct the fully-qualified reference from the subject reference and the artifact digest.
            string registry = digest[..digest.IndexOf('/')];
            string repository = digest[(digest.IndexOf('/') + 1)..digest.IndexOf('@')];
            string artifactReference = $"{registry}/{repository}@{artifactDigest}";

            var referrerInfo = new ReferrerInfo(artifactReference, OciArtifactType.Lifecycle)
            {
                Annotations = annotations
            };

            return new LifecycleArtifact(referrerInfo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to annotate EOL for digest '{Digest}'", digest);
            return null;
        }
    }
}
