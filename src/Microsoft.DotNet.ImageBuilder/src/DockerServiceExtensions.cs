// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public static class DockerServiceExtensions
    {
        public static async Task ExecuteWithUserAsync(this IDockerService dockerService, Func<Task> action, string? username, string? password,
            string? server, bool isDryRun)
        {
            bool userSpecified = username != null;
            if (userSpecified)
            {
                dockerService.Login(username!, password!, server, isDryRun);
            }

            try
            {
                await action();
            }
            finally
            {
                if (userSpecified)
                {
                    dockerService.Logout(server, isDryRun);
                }
            }
        }
    }
}
#nullable disable
