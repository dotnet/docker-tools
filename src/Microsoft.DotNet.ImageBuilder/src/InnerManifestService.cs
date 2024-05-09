// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public interface IManifestService
    {
        Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun);
        Task<IEnumerable<string>> GetImageLayersAsync(string tag, bool isDryRun);
        Task<string?> GetImageDigestAsync(string image, bool isDryRun);
        Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun);
    }

    // Wrapper for ManifestService extension methods required for unit testing 
    public class ManifestService(IInnerManifestService inner) : IManifestService
    {
        private readonly IInnerManifestService _inner = inner;

        public Task<string?> GetImageDigestAsync(string image, bool isDryRun) =>
            _inner.GetImageDigestAsync(image, isDryRun);

        public Task<IEnumerable<string>> GetImageLayersAsync(string tag, bool isDryRun) =>
            _inner.GetImageLayersAsync(tag, isDryRun);

        public Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun) =>
            _inner.GetManifestAsync(image, isDryRun);

        public Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun) =>
            _inner.GetManifestDigestShaAsync(tag, isDryRun);
    }

    internal class InnerManifestService : IInnerManifestService
    {
        private readonly IRegistryContentClientFactory _registryClientFactory;
        private readonly string? _ownedAcr;
        private readonly IRegistryCredentialsHost? _credsHost;

        public InnerManifestService(
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
}
