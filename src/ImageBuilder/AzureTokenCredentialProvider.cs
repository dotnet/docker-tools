// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

internal class AzureTokenCredentialProvider : IAzureTokenCredentialProvider
{
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, TokenCredential> _credentialsCache = [];
    private readonly string _systemAccessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN") ?? "";

    /// <summary>
    /// Get a TokenCredential for the specified service connection. Only works in the context of an Azure Pipeline.
    /// Ensure that the SYSTEM_ACCESSTOKEN environment variable is set in the pipeline.
    /// </summary>
    /// <remarks>
    /// The returned TokenCredential handles token caching and refresh automatically via the Azure SDK.
    /// Tokens are refreshed when they are close to expiring, so long-running operations will not fail
    /// due to token expiration.
    /// </remarks>
    /// <param name="serviceConnection">Details about the Azure DevOps service connection to use.</param>
    /// <param name="scope">The scope to request for the token. This parameter is ignored; the credential
    /// will request the appropriate scope when GetToken is called.</param>
    /// <returns>A <see cref="TokenCredential"/> that can be used to authenticate to Azure services.</returns>
    public TokenCredential GetCredential(
        IServiceConnection? serviceConnection,
        string scope = AzureScopes.DefaultAzureManagementScope)
    {
        // Cache by service connection ID only. The TokenCredential handles different scopes internally.
        string cacheKey = serviceConnection?.Id ?? "default";

        return LockHelper.DoubleCheckedLockLookup(
            lockObj: _cacheLock,
            dictionary: _credentialsCache,
            key: cacheKey,
            getValue: () =>
            {
                TokenCredential? credential = null;

                if (serviceConnection is not null
                    // System.CommandLine can instantiate this class with default values (null) when it is not provided
                    // on the command line, so we need to check for null values.
                    && !string.IsNullOrEmpty(serviceConnection.ClientId)
                    && !string.IsNullOrEmpty(serviceConnection.TenantId)
                    && !string.IsNullOrEmpty(serviceConnection.Id))
                {
                    if (string.IsNullOrWhiteSpace(_systemAccessToken))
                    {
                        throw new InvalidOperationException(
                            $"""
                            Attempted to get Service Connection credential but SYSTEM_ACCESSTOKEN environment variable was not set.
                            Service connection details: {serviceConnection}
                            """);
                    }

                    credential = new AzurePipelinesCredential(
                        serviceConnection.TenantId,
                        serviceConnection.ClientId,
                        serviceConnection.Id,
                        _systemAccessToken);
                }

#if DEBUG
                // Fall back to DefaultAzureCredential if no service connection is provided.
                // This can still be used for local development against non-production resources.
                credential ??= new DefaultAzureCredential(); // CodeQL [SM05137] Safe for DEBUG builds only, used for local development against non-production resources
#endif

                if (credential is null)
                {
                    // Using DefaultAzureCredential is not allowed in production environments.
                    throw new InvalidOperationException(
                        "Attempted to get an Azure Pipelines Credential but no service connection was provided."
                    );
                }

                // Wrap the credential with CachingTokenCredential to ensure tokens are cached.
                // AzurePipelinesCredential does not cache tokens internally, so each call to
                // GetToken would make a new request to Azure, which is slow. The caching wrapper
                // caches the token and refreshes it only when it's close to expiration.
                return new CachingTokenCredential(credential);
            });
    }
}
