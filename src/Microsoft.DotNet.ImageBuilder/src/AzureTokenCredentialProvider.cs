// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IAzureTokenCredentialProvider))]
internal class AzureTokenCredentialProvider : IAzureTokenCredentialProvider
{
    private readonly Dictionary<string, TokenCredential?> _credentialsByScope;

    [ImportingConstructor]
    public AzureTokenCredentialProvider(ILoggerService loggerService)
    {
        loggerService.WriteSubheading("Initializing Azure credentials");

        // Pre-cache the credentials for all the scopes we need. This is done at startup to ensure we have a valid OIDC token from the pipeline before it expires.
        // Otherwise, long-running commands that don't attempt to request an Azure access token until much later run the risk of failing due to the expired OIDC token.
        _credentialsByScope = AuthHelper.AllScopes
            .ToDictionary<string, string, TokenCredential?>(scope => scope, scope =>
            {
                TokenCredential? credential = null;

                // If debugging on a dev machine, use DefaultAzureCredential which will use whatever method available for authenticating with Azure (e.g. Azure CLI).
                // This allows the developer's identity to be used to authenticate to the resource. Otherwise, assume this is being run in the context of an AzDO
                // pipeline.
#if DEBUG
                loggerService.WriteMessage($"Getting default credentials for scope '{scope}'");
                credential = new DefaultAzureCredential();
#else
                // When running in the context of an AzDO pipeline, the AZURE_FEDERATED_TOKEN_FILE env var will be set when the step is configured to use
                // Azure authentication. If this var is not set, the step is not configured for authentication and no credential should be associated with the
                // scope. This ensures that an exception will be thrown when retrieving the credential if an attempt is made to authenticate.
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FEDERATED_TOKEN_FILE")))
                {
                    loggerService.WriteMessage($"Getting token for scope '{scope}'");
                    AccessToken token = new WorkloadIdentityCredential().GetToken(new TokenRequestContext([scope]), CancellationToken.None);
                    credential = new StaticTokenCredential(token);
                }
                else
                {
                    loggerService.WriteMessage($"No credentials set for scope '{scope}'");
                }
#endif

                return credential;
            });
    }

    public TokenCredential GetCredential(string scope = AuthHelper.DefaultAzureManagementScope)
    {
        if (!_credentialsByScope.TryGetValue(scope, out TokenCredential? credential) || credential is null)
        {
            throw new Exception($"A credential for scope '{scope}' has not been set.");
        }

        return credential;
    }

    private class StaticTokenCredential(AccessToken accessToken) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => accessToken;
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(accessToken);
    }
}
