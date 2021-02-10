// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class DockerRegistryCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
        where TOptions : DockerRegistryOptions, new()
        where TOptionsBuilder : DockerRegistryOptionsBuilder, new()
    {
        protected void ExecuteWithUser(Action action)
        {
            Options.CredentialsOptions.Credentials.TryGetValue(Manifest.Registry ?? "", out RegistryCredentials? credentials);
            DockerHelper.ExecuteWithUser(action, credentials?.Username, credentials?.Password, Manifest.Registry, Options.IsDryRun);
        }
    }
}
#nullable disable
