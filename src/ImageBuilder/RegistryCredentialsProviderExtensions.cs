// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.ImageBuilder.Commands;

namespace Microsoft.DotNet.DockerTools.ImageBuilder;

#nullable enable
internal static class RegistryCredentialsProviderExtensions
{
    public static async Task ExecuteWithCredentialsAsync(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        Func<Task> action,
        RegistryCredentialsOptions credentialsOptions,
        string registryName,
        string? ownedAcr)
    {
        bool loggedIn = await credsProvider.LogInToRegistry(
            isDryRun,
            credentialsOptions,
            registryName,
            ownedAcr);

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
        RegistryCredentialsOptions credentialsOptions,
        string registryName,
        string? ownedAcr)
    {
        await credsProvider.ExecuteWithCredentialsAsync(
            isDryRun,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            credentialsOptions,
            registryName,
            ownedAcr
        );
    }

    private static async Task<bool> LogInToRegistry(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        RegistryCredentialsOptions credentialsOptions,
        string registryName,
        string? ownedAcr)
    {
        bool loggedIn = false;

        RegistryCredentials? credentials = null;
        if (!isDryRun)
        {
            credentials = await credsProvider.GetCredentialsAsync(registryName, ownedAcr, credentialsOptions);
        }

        if (!string.IsNullOrEmpty(registryName) && credentials is not null)
        {
            DockerHelper.Login(credentials, registryName, isDryRun);
            loggedIn = true;
        }

        return loggedIn;
    }
}
