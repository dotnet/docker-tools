// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

internal class ManifestService(
    IRegistryManifestClientFactory registryClientFactory,
    IRegistryCredentialsHost? credsHost = null
) : IManifestService
{
    private readonly IRegistryManifestClientFactory _registryClientFactory = registryClientFactory;
    private readonly IRegistryCredentialsHost? _credsHost = credsHost;

    public Task<ManifestQueryResult> GetManifestAsync(ImageName image, bool isDryRun)
    {
        if (isDryRun)
        {
            return Task.FromResult(new ManifestQueryResult("", new JsonObject()));
        }

        IRegistryManifestClient registryClient = _registryClientFactory.Create(image.Registry, image.Repo, _credsHost);

        string tagOrDigest = !string.IsNullOrEmpty(image.Tag) ? image.Tag : image.Digest;
        return registryClient.GetManifestAsync(tagOrDigest);
    }
}
