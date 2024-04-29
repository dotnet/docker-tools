// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IManifestService))]
    public class ManifestService : IManifestService
    {
        private readonly IRegistryClientFactory _registryClientFactory;

        [ImportingConstructor]
        public ManifestService(IRegistryClientFactory registryClientFactory)
        {
            _registryClientFactory = registryClientFactory;
        }

        public Task<ManifestQueryResult> GetManifestAsync(string image, IRegistryCredentialsHost credsHost, bool isDryRun)
        {
            if (isDryRun)
            {
                return Task.FromResult(new ManifestQueryResult("", new JsonObject()));
            }

            ImageName imageName = ImageName.Parse(image, autoResolveImpliedNames: true);

            IRegistryClient registryClient = _registryClientFactory.Create(imageName.Registry!, credsHost);
            return registryClient.GetManifestAsync(imageName.Repo, (imageName.Tag ?? imageName.Digest)!);
        }
    }
}
#nullable disable
