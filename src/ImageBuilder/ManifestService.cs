// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder;

internal class ManifestService : IManifestService
{
    private readonly IRegistryContentClientFactory _registryClientFactory;
    private readonly string? _ownedAcr;
    private readonly IRegistryCredentialsHost? _credsHost;

    public ManifestService(
        IRegistryContentClientFactory registryClientFactory,
        string? ownedAcr = null,
        IRegistryCredentialsHost? credsHost = null)
    {
        _registryClientFactory = registryClientFactory;
        _ownedAcr = ownedAcr;
        _credsHost = credsHost;
    }

    public Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun)
    {
        if (isDryRun)
        {
            return Task.FromResult(new ManifestQueryResult("", new JsonObject()));
        }

        ImageName imageName = ImageName.Parse(image, autoResolveImpliedNames: true);

        IRegistryContentClient registryClient = _registryClientFactory.Create(
            imageName.Registry!, imageName.Repo, _ownedAcr, _credsHost);
        return registryClient.GetManifestAsync((imageName.Tag ?? imageName.Digest)!);
    }
}
