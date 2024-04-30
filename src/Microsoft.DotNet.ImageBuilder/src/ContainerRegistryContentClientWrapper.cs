// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure;
using Azure.Containers.ContainerRegistry;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal class ContainerRegistryContentClientWrapper : IRegistryContentClient
{
    private readonly ContainerRegistryContentClient _innerClient;

    public ContainerRegistryContentClientWrapper(ContainerRegistryContentClient innerClient)
    {
        _innerClient = innerClient;
    }

    public async Task<ManifestQueryResult> GetManifestAsync(string tagOrDigest)
    {
        Response<GetManifestResult> result = await _innerClient.GetManifestAsync(tagOrDigest);
        JsonObject manifestData = (JsonObject)JsonNode.Parse(result.Value.Manifest.ToString());
        return new ManifestQueryResult(result.Value.Digest, manifestData);
    }
}
