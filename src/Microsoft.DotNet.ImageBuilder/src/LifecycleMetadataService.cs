// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Models.Oras;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

[Export(typeof(ILifecycleMetadataService))]
public class LifecycleMetadataService : ILifecycleMetadataService
{
    public const string EndOfLifeAnnotation = "vnd.microsoft.artifact.lifecycle.end-of-life.date";
    public const string EolDateFormat = "yyyy-MM-dd";
    private const string LifecycleArtifactType = "application/vnd.microsoft.artifact.lifecycle";

    private readonly IOrasClient _orasClient;

    [ImportingConstructor]
    public LifecycleMetadataService(IOrasClient orasClient)
    {
        _orasClient = orasClient;
    }

    public bool IsDigestAnnotatedForEol(string digest, ILoggerService loggerService, bool isDryRun, [MaybeNullWhen(false)] out Manifest lifecycleArtifactManifest)
    {
        string stdOut = _orasClient.RunOrasCommand(
            args: ["discover",  $"--artifact-type {LifecycleArtifactType}", digest],
            isDryRun: isDryRun);

        if (LifecycleAnnotationExists(stdOut, loggerService, out lifecycleArtifactManifest))
        {
            return true;
        }

        lifecycleArtifactManifest = null;
        return false;
    }

    public bool AnnotateEolDigest(string digest, DateOnly date, ILoggerService loggerService, bool isDryRun, [MaybeNullWhen(false)] out Manifest lifecycleArtifactManifest)
    {
        try
        {
            string output = _orasClient.RunOrasCommand(
                args: [
                    "attach",
                    $"--artifact-type {LifecycleArtifactType}",
                    $"--annotation \"{EndOfLifeAnnotation}={date.ToString(EolDateFormat)}\"",
                    digest
                ],
                isDryRun: isDryRun);

            lifecycleArtifactManifest = JsonConvert.DeserializeObject<Manifest>(output ?? string.Empty) ?? throw new Exception("Unable to deserialize manifest");
        }
        catch (InvalidOperationException ex)
        {
            loggerService.WriteError($"Failed to annotate EOL for digest '{digest}': {ex.Message}");
            lifecycleArtifactManifest = null;
            return false;
        }

        return true;
    }

    private static bool LifecycleAnnotationExists(string json, ILoggerService loggerService, [MaybeNullWhen(false)] out Manifest lifecycleArtifactManifest)
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
            loggerService.WriteError($"Failed to deserialize 'oras discover' json: {ex.Message}");
        }

        lifecycleArtifactManifest = null;
        return false;
    }

}
