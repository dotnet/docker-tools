// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IAzureTokenCredentialProvider))]
internal class AzureTokenCredentialProvider : IAzureTokenCredentialProvider
{
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, TokenCredential?> _credentialsCache = [];
    private readonly string _systemAccessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN") ?? "";

    /// <summary>
    /// Get a TokenCredential for the specified service connection. Only works in the context of an Azure Pipeline.
    /// Ensure that the SYSTEM_ACCESSTOKEN environment variable is set in the pipeline.
    /// </summary>
    /// <param name="serviceConnection">Details about the Azure DevOps service connection to use.</param>
    /// <param name="scope">The scope to request for the token. This might be a URL or a GUID.</param>
    /// <returns>A <see cref="TokenCredential"/> that can be used to authenticate to Azure services.</returns>
    public TokenCredential GetCredential(
        IServiceConnection? serviceConnection,
        string scope = AzureScopes.DefaultAzureManagementScope)
    {
        string cacheKey = $"{serviceConnection?.ServiceConnectionId}:{scope}";

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
                    && !string.IsNullOrEmpty(serviceConnection.ServiceConnectionId))
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
                        serviceConnection.ServiceConnectionId,
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

                var accessToken = credential.GetToken(new TokenRequestContext([scope]), CancellationToken.None);
                return new StaticTokenCredential(accessToken);
            });
    }

    private class StaticTokenCredential(AccessToken accessToken) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => accessToken;
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(accessToken);
    }
}
