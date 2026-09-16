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
        Func<CancellationToken, Task> action,
        IRegistryCredentialsHost credentialsOptions,
        string registryName,
        CancellationToken cancellationToken)
    {
        bool loggedIn = await LogInToRegistry(credsProvider, isDryRun, credentialsOptions, registryName, cancellationToken);

        try
        {
            await action(cancellationToken);
        }
        finally
        {
            if (loggedIn && !string.IsNullOrEmpty(registryName))
            {
                DockerHelper.Logout(registryName, isDryRun, cancellationToken.IsCancellationRequested ? CancellationToken.None : cancellationToken);
            }
        }
    }

    public static async Task ExecuteWithCredentialsAsync(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        Action<CancellationToken> action,
        IRegistryCredentialsHost credentialsOptions,
        string registryName,
        CancellationToken cancellationToken)
    {
        await credsProvider.ExecuteWithCredentialsAsync(
            isDryRun,
            ct => {
                action(ct);
                return Task.CompletedTask;
            },
            credentialsOptions,
            registryName,
            cancellationToken);
    }

    private static async Task<bool> LogInToRegistry(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        IRegistryCredentialsHost credentialsOptions,
        string registryName,
        CancellationToken cancellationToken)
    {
        bool loggedIn = false;

        RegistryCredentials? credentials = null;
        if (!isDryRun)
        {
            credentials = await credsProvider.GetCredentialsAsync(registryName, credentialsOptions, cancellationToken);
        }

        if (!string.IsNullOrEmpty(registryName) && credentials is not null)
        {
            DockerHelper.Login(credentials, registryName, isDryRun, cancellationToken);
            loggedIn = true;
        }

        return loggedIn;
    }
}
