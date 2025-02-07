// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using Microsoft;


// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using Microsoft.DotNet.DockerTools.ImageBuilder;

namespace Microsoft.DotNet.DockerTools.ImageBuilder
{
    public interface IManifestServiceFactory
    {
        IManifestService Create(string? ownedAcr = null, IRegistryCredentialsHost? credsHost = null);
    }
}
