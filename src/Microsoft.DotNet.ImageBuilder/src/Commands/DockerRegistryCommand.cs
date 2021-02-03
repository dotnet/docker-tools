// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class DockerRegistryCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
        where TOptions : DockerRegistryOptions, new()
        where TOptionsBuilder : DockerRegistryOptionsBuilder, new()
    {
        public DockerRegistryCommand(IDockerService dockerService)
            : base(dockerService)
        {
        }

        protected Task ExecuteWithUserAsync(Func<Task> action) =>
            DockerService.ExecuteWithUserAsync(action, Options.Username, Options.Password, Manifest.Registry, Options.IsDryRun);
    }
}
