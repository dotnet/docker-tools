// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface IManifestService
{
    Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun);

    public async Task<IEnumerable<string>> GetImageLayersAsync(string tag, bool isDryRun)
    {
        if (isDryRun)
        {
            return [];
        }

        ManifestQueryResult manifestResult = await GetManifestAsync(tag, isDryRun);
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

    public async Task<string?> GetLocalImageDigestAsync(string image, bool isDryRun)
    {
        IEnumerable<string> digests = DockerHelper.GetImageDigests(image, isDryRun);

        // A digest will not exist for images that have been built locally or have been manually installed
        if (!digests.Any())
        {
            return null;
        }

        string digestSha = await GetManifestDigestShaAsync(image, isDryRun);
        if (digestSha is null)
        {
            return null;
        }

        string digest = DockerHelper.GetDigestString(DockerHelper.GetRepo(image), digestSha);
        if (!digests.Contains(digest))
        {
            throw new InvalidOperationException(
                $"Found published digest '{digestSha}' for tag '{image}' but could not find a matching digest value from " +
                $"the set of locally pulled digests for this tag: { string.Join(", ", digests) }. This most likely means that " +
                "this tag has been updated since it was last pulled.");
        }

        return digest;
    }

    public async Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun)
    {
        ManifestQueryResult manifestResult = await GetManifestAsync(tag, isDryRun);
        return manifestResult.ContentDigest;
    }
}
