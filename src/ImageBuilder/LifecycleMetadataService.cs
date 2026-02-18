// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Models.Oras;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder;
public class LifecycleMetadataService : ILifecycleMetadataService
{
    public const string EndOfLifeAnnotation = "vnd.microsoft.artifact.lifecycle.end-of-life.date";
    public const string EolDateFormat = "yyyy-MM-dd";
    private const string LifecycleArtifactType = "application/vnd.microsoft.artifact.lifecycle";

    private readonly IOrasClient _orasClient;

    public LifecycleMetadataService(IOrasClient orasClient)
    {
        _orasClient = orasClient;
    }

    public bool IsDigestAnnotatedForEol(string digest, ILogger logger, bool isDryRun, [MaybeNullWhen(false)] out Manifest lifecycleArtifactManifest)
    {
        string stdOut = _orasClient.RunOrasCommand(
            args: [
                "discover",
                $"--artifact-type {LifecycleArtifactType}",
                $"--format json",
                digest
            ],
            isDryRun: isDryRun);

        if (LifecycleAnnotationExists(stdOut, logger, out lifecycleArtifactManifest))
        {
            return true;
        }

        lifecycleArtifactManifest = null;
        return false;
    }

    public bool AnnotateEolDigest(string digest, DateOnly date, ILogger logger, bool isDryRun, [MaybeNullWhen(false)] out Manifest lifecycleArtifactManifest)
    {
        try
        {
            string output = _orasClient.RunOrasCommand(
                args: [
                    "attach",
                    $"--artifact-type {LifecycleArtifactType}",
                    $"--annotation \"{EndOfLifeAnnotation}={date.ToString(EolDateFormat)}\"",
                    $"--format json",
                    digest
                ],
                isDryRun: isDryRun);

            if (isDryRun)
            {
                lifecycleArtifactManifest = null;
                return false;
            }

            lifecycleArtifactManifest = JsonConvert.DeserializeObject<Manifest>(output ?? string.Empty)
                ?? throw new Exception(
                    $"""
                    Unable to deserialize lifecycle metadata manifest from 'oras' output:

                    {output}

                    """
                );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to annotate EOL for digest '{Digest}'", digest);
            lifecycleArtifactManifest = null;
            return false;
        }

        return true;
    }

    private static bool LifecycleAnnotationExists(string json, ILogger logger, [MaybeNullWhen(false)] out Manifest lifecycleArtifactManifest)
    {
        try
        {
            OrasDiscoverData? orasDiscoverData = JsonConvert.DeserializeObject<OrasDiscoverData>(json);
            if (orasDiscoverData?.Manifests != null)
            {
                lifecycleArtifactManifest = orasDiscoverData.Manifests.FirstOrDefault(m => m.ArtifactType == LifecycleArtifactType);
                return lifecycleArtifactManifest is not null;
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize 'oras discover' json");
        }

        lifecycleArtifactManifest = null;
        return false;
    }

}
