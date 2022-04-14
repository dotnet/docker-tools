// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public static class ManifestServiceExtensions
    {
        public static async Task<IEnumerable<string>> GetImageLayersAsync(
            this IManifestService manifestService, string tag, IRegistryCredentialsHost credsHost, bool isDryRun)
        {
            ManifestResult manifestResult = await manifestService.GetManifestAsync(tag, credsHost, isDryRun);

            if (isDryRun)
            {
                return Enumerable.Empty<string>();
            }

            if (!manifestResult.Manifest.ContainsKey("layers"))
            {
                JsonArray manifests = (JsonArray)(manifestResult.Manifest["manifests"] ??
                    throw new InvalidOperationException("Expected manifests property"));
                throw new InvalidOperationException(
                    $"'{tag}' is expected to be a concrete tag with 1 manifest. It has '{manifests.Count}' manifests.");
            }

            return ((JsonArray)manifestResult.Manifest["layers"]!)
                .Select(layer => (layer!["digest"] ?? throw new InvalidOperationException("Expected digest property")).ToString())
                .Reverse();
        }

        public static async Task<string> GetManifestDigestShaAsync(
            this IManifestService manifestService, string tag, IRegistryCredentialsHost credsHost, bool isDryRun)
        {
            ManifestResult manifestResult = await manifestService.GetManifestAsync(tag, credsHost, isDryRun);
            return manifestResult.ContentDigest;
        }
    }
}
#nullable disable
