// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record PublishConfiguration
{
    public static string ConfigurationKey => nameof(PublishConfiguration);

    /// <summary>
    /// Images are built and pushed into this registry before testing and publishing.
    /// </summary>
    public RegistryEndpoint? BuildRegistry { get; set; }

    /// <summary>
    /// Images are copied from <see cref="BuildRegistry"/> to this registry during publishing.
    /// </summary>
    public RegistryEndpoint? PublishRegistry { get; set; }

    /// <summary>
    /// Registry that holds mirrored copies of external base images. The pipeline templates
    /// select the correct mirror (internal staging vs. public mirror) at template-compile
    /// time based on the AzDO team project, so the app sees a single registry to redirect
    /// external base-image lookups to without any runtime conditionals.
    /// </summary>
    public RegistryEndpoint? MirrorRegistry { get; set; }

    /// <summary>
    /// Configuration for container image signing via ESRP.
    /// </summary>
    public SigningConfiguration? Signing { get; set; }

    /// <summary>
    /// Service connection used for ACR cleanup operations (e.g., deleting images and repos).
    /// This is separate from the per-registry service connections in <see cref="RegistryAuthentication"/>
    /// because cleanup operations may require different RBAC permissions.
    /// </summary>
    public ServiceConnection? CleanServiceConnection { get; set; }

    /// <summary>
    /// Authentication details for container registries.
    /// </summary>
    /// <remarks>
    /// Each entry should have a Server property set to the registry server address
    /// (e.g., "myregistry.azurecr.io"). Multiple registry endpoints can share the
    /// same authentication if they point to the same server.
    /// </remarks>
    public List<RegistryAuthentication> RegistryAuthentication { get; set; } = [];

    /// <summary>
    /// Gets all registry endpoints that were provided in the publish configuration.
    /// </summary>
    public IEnumerable<RegistryEndpoint> GetKnownRegistries()
    {
        RegistryEndpoint?[] registries =
        [
            BuildRegistry,
            PublishRegistry,
            MirrorRegistry
        ];

        // Use OfType to filter out null values, since Where(x => x is not null)
        // does not get rid of the nullable annotation on RegistryEndpoint?.
        return registries.OfType<RegistryEndpoint>();
    }
}
