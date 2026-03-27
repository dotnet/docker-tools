// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder;

public class LifecycleMetadataService : ILifecycleMetadataService
{
    public const string EndOfLifeAnnotation = "vnd.microsoft.artifact.lifecycle.end-of-life.date";
    public const string EolDateFormat = "yyyy-MM-dd";

    private readonly IOrasService _orasService;
    private readonly ILogger<LifecycleMetadataService> _logger;

    public LifecycleMetadataService(IOrasService orasService, ILogger<LifecycleMetadataService> logger)
    {
        _orasService = orasService;
        _logger = logger;
    }

    public async Task<Manifest?> IsDigestAnnotatedForEolAsync(string digest, CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<ReferrerInfo> referrers = await _orasService.GetReferrersAsync(digest, cancellationToken);

            ReferrerInfo? lifecycleReferrer = referrers.FirstOrDefault(
                r => r.ArtifactType == OciArtifactType.Lifecycle);

            if (lifecycleReferrer is null)
            {
                return null;
            }

            return new Manifest
            {
                ArtifactType = lifecycleReferrer.ArtifactType ?? string.Empty,
                Reference = lifecycleReferrer.Digest,
                Annotations = lifecycleReferrer.Annotations is not null
                    ? new Dictionary<string, string>(lifecycleReferrer.Annotations)
                    : []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check EOL annotation for digest '{Digest}'", digest);
            return null;
        }
    }

    public async Task<Manifest?> AnnotateEolDigestAsync(string digest, DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string> annotations = new()
            {
                [EndOfLifeAnnotation] = date.ToString(EolDateFormat)
            };

            string artifactDigest = await _orasService.AttachArtifactAsync(
                digest,
                OciArtifactType.Lifecycle,
                annotations,
                cancellationToken);

            // Construct the fully-qualified reference from the subject reference and the artifact digest.
            string registry = digest[..digest.IndexOf('/')];
            string repository = digest[(digest.IndexOf('/') + 1)..digest.IndexOf('@')];
            string artifactReference = $"{registry}/{repository}@{artifactDigest}";

            return new Manifest
            {
                ArtifactType = OciArtifactType.Lifecycle,
                Reference = artifactReference,
                Annotations = annotations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to annotate EOL for digest '{Digest}'", digest);
            return null;
        }
    }
}
