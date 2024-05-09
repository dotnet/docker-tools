// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class DockerRegistryCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
        where TOptions : DockerRegistryOptions, new()
        where TOptionsBuilder : DockerRegistryOptionsBuilder, new()
    {
        protected IRegistryCredentialsProvider RegistryCredentialsProvider { get; init; }

        protected DockerRegistryCommand(IRegistryCredentialsProvider registryCredentialsProvider)
        {
            RegistryCredentialsProvider = registryCredentialsProvider;
        }

        protected async Task ExecuteWithCredentialsAsync(bool isDryRun, Func<Task> action, string registryName, string? ownedAcr)
        {
            bool loggedIn = false;

            RegistryCredentials? credentials = await RegistryCredentialsProvider.GetCredentialsAsync(
                    registryName, ownedAcr, Options.CredentialsOptions);

            if (registryName is not null && credentials is not null)
            {
                DockerHelper.Login(credentials, registryName, isDryRun);
                loggedIn = true;
            }

            try
            {
                await action();
            }
            finally
            {
                if (loggedIn && registryName is not null)
                {
                    DockerHelper.Logout(registryName, isDryRun);
                }
            }
        }

    }
}
#nullable disable
