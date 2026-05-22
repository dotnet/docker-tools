// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;

namespace Microsoft.DotNet.ImageBuilder;

internal class ManifestService(IOrasService orasService) : IManifestService
{
    private readonly IOrasService _orasService = orasService;

    public Task<ManifestQueryResult> GetManifestAsync(ImageName image, bool isDryRun)
    {
        if (isDryRun)
        {
            return Task.FromResult(new ManifestQueryResult("", new JsonObject()));
        }

        // Use the image's logical registry (e.g., "docker.io") in the ORAS
        // reference. ORAS internally maps "docker.io" to its API host
        // "registry-1.docker.io" for transport, and our credential adapter
        // normalizes credential lookups back to "docker.io".
        string reference = !string.IsNullOrEmpty(image.Tag)
            ? $"{image.Registry}/{image.Repo}:{image.Tag}"
            : $"{image.Registry}/{image.Repo}@{image.Digest}";

        return _orasService.GetManifestAsync(reference);
    }
}
