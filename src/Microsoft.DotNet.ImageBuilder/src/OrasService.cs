// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IOrasService))]
    public class OrasService : IOrasService
    {
        private const string LifecycleArtifactType = "application/vnd.microsoft.artifact.lifecycle";
        public const string EndOfLifeAnnotation = "vnd.microsoft.artifact.lifecycle.end-of-life.date";
        public const string EolDateFormat = "yyyy-MM-dd";

        public bool IsDigestAnnotatedForEol(string digest, ILoggerService loggerService, bool isDryRun, [MaybeNullWhen(false)] out OciManifest lifecycleArtifactManifest)
        {
            string? stdOut = ExecuteHelper.ExecuteWithRetry(
                "oras",
                $"discover --artifact-type {LifecycleArtifactType} {digest} --format json",
                isDryRun);

            if (!string.IsNullOrEmpty(stdOut) && LifecycleAnnotationExists(stdOut, loggerService, out lifecycleArtifactManifest))
            {
                return true;
            }

            lifecycleArtifactManifest = null;
            return false;
        }

        public bool AnnotateEolDigest(string digest, DateOnly date, ILoggerService loggerService, bool isDryRun)
        {
            try
            {
                ExecuteHelper.ExecuteWithRetry(
                    "oras",
                    $"attach --artifact-type {LifecycleArtifactType} --annotation \"{EndOfLifeAnnotation}={date.ToString(EolDateFormat)}\" {digest}",
                    isDryRun);
            }
            catch (InvalidOperationException ex)
            {
                loggerService.WriteError($"Failed to annotate EOL for digest '{digest}': {ex.Message}");
                return false;
            }

            return true;
        }

        private static bool LifecycleAnnotationExists(string json, ILoggerService loggerService, [MaybeNullWhen(false)] out OciManifest lifecycleArtifactManifest)
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
}
