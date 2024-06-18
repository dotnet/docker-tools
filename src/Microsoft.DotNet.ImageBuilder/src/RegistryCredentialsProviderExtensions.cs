﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal static class RegistryCredentialsProviderExtensions
{
    public static async Task ExecuteWithCredentialsAsync(this IRegistryCredentialsProvider credsProvider, bool isDryRun, Func<Task> action, RegistryCredentialsOptions credentialsOptions, string registryName, string? ownedAcr)
    {
        bool loggedIn = false;

        RegistryCredentials? credentials = await credsProvider.GetCredentialsAsync(
                registryName, ownedAcr, credentialsOptions);

        if (!string.IsNullOrEmpty(registryName) && credentials is not null)
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
            if (loggedIn && !string.IsNullOrEmpty(registryName))
            {
                DockerHelper.Logout(registryName, isDryRun);
            }
        }
    }
}
