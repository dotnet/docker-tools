// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public class ManifestServiceFactory(IRegistryManifestClientFactory registryClientFactory) : IManifestServiceFactory
    {
        private readonly IRegistryManifestClientFactory _registryClientFactory = registryClientFactory;

        public IManifestService Create(IRegistryCredentialsHost? credsHost = null)
        {
            return new ManifestService(_registryClientFactory, credsHost);
        }
    }
}
