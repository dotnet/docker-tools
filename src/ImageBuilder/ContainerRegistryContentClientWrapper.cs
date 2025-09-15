// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure;
using Azure.Containers.ContainerRegistry;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public class ContainerRegistryContentClientWrapper(ContainerRegistryContentClient innerClient) : IContainerRegistryContentClient
{
    private readonly ContainerRegistryContentClient _innerClient = innerClient;

    public string RepositoryName => _innerClient.RepositoryName;

    public async Task<ManifestQueryResult> GetManifestAsync(string tagOrDigest)
    {
        Response<GetManifestResult> result = await _innerClient.GetManifestAsync(tagOrDigest);
        JsonObject manifestData = (JsonObject)(JsonNode.Parse(result.Value.Manifest.ToString()) ?? throw new JsonException($"Unable to deserialize result: {result.Value.Manifest}"));
        return new ManifestQueryResult(result.Value.Digest, manifestData);
    }

    public Task DeleteManifestAsync(string tagOrDigest) => _innerClient.DeleteManifestAsync(tagOrDigest);
}
