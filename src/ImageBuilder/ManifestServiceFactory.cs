// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Oras;

namespace Microsoft.DotNet.ImageBuilder
{
    public class ManifestServiceFactory(IOrasServiceFactory orasServiceFactory) : IManifestServiceFactory
    {
        private readonly IOrasServiceFactory _orasServiceFactory = orasServiceFactory;

        public IManifestService Create(IRegistryCredentialsHost? credsHost = null)
        {
            return new ManifestService(_orasServiceFactory.Create(credsHost));
        }
    }
}
