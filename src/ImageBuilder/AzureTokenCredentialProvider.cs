// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    private readonly Dictionary<string, TokenCredential?> _credentialsByScope = [];

    public TokenCredential GetCredential(string scope = AzureScopes.DefaultAzureManagementScope) =>
        LockHelper.DoubleCheckedLockLookup(
            _cacheLock,
            _credentialsByScope,
            scope,
            () =>
            {
                AccessToken token = new DefaultAzureCredential().GetToken(new TokenRequestContext([scope]), CancellationToken.None);
                return new StaticTokenCredential(token);
            });

    private class StaticTokenCredential(AccessToken accessToken) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => accessToken;
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(accessToken);
    }
}
