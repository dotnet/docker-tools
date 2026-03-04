// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

internal static class RegistryCredentialsProviderExtensions
{
    public static async Task ExecuteWithCredentialsAsync(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        Func<Task> action,
        IRegistryCredentialsHost credentialsOptions,
        string registryName)
    {
        bool loggedIn = await LogInToRegistry(credsProvider, isDryRun, credentialsOptions, registryName);

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

    public static async Task ExecuteWithCredentialsAsync(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        Action action,
        IRegistryCredentialsHost credentialsOptions,
        string registryName)
    {
        await credsProvider.ExecuteWithCredentialsAsync(
            isDryRun,
            () => {
                action();
                return Task.CompletedTask;
            },
            credentialsOptions,
            registryName);
    }

    private static async Task<bool> LogInToRegistry(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        IRegistryCredentialsHost credentialsOptions,
        string registryName)
    {
        bool loggedIn = false;

        RegistryCredentials? credentials = null;
        if (!isDryRun)
        {
            credentials = await credsProvider.GetCredentialsAsync(registryName, credentialsOptions);
        }

        if (!string.IsNullOrEmpty(registryName) && credentials is not null)
        {
            DockerHelper.Login(credentials, registryName, isDryRun);
            loggedIn = true;
        }

        return loggedIn;
    }
}
