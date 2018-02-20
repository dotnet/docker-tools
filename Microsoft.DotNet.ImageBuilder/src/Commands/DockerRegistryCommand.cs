// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class DockerRegistryCommand<TOptions> : Command<TOptions>
        where TOptions : DockerRegistryOptions, new()
    {
        protected void ExecuteWithUser(Action action)
        {
            bool userSpecified = Options.Username != null;
            if (userSpecified)
            {
                DockerHelper.Login(Options.Username, Options.Password, Options.Server, Options.IsDryRun);
            }

            try
            {
                action();
            }
            finally
            {
                if (userSpecified)
                {
                    DockerHelper.Logout(Options.Server, Options.IsDryRun);
                }
            }
        }
    }
}
