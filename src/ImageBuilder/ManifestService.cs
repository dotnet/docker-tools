// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;

namespace Microsoft.DotNet.ImageBuilder;

internal class ManifestService(
    IOrasService orasService,
    IRegistryResolver registryResolver
) : IManifestService
{
    private readonly IOrasService _orasService = orasService;
    private readonly IRegistryResolver _registryResolver = registryResolver;

    public Task<ManifestQueryResult> GetManifestAsync(ImageName image, bool isDryRun)
    {
        if (isDryRun)
        {
            return Task.FromResult(new ManifestQueryResult("", new JsonObject()));
        }

        // Resolve the effective registry endpoint. This maps Docker Hub's
        // user-facing name (docker.io) to its API hostname (registry-1.docker.io)
        // and leaves other registries unchanged.
        RegistryInfo registryInfo = _registryResolver.Resolve(image.Registry, credsHost: null);

        string reference = !string.IsNullOrEmpty(image.Tag)
            ? $"{registryInfo.EffectiveRegistry}/{image.Repo}:{image.Tag}"
            : $"{registryInfo.EffectiveRegistry}/{image.Repo}@{image.Digest}";

        return _orasService.GetManifestAsync(reference);
    }
}
