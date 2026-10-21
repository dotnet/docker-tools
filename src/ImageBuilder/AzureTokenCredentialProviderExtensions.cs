// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

internal static class AzureTokenCredentialProviderExtensions
{
    public static ValueTask<AccessToken> GetTokenAsync(
        this IAzureTokenCredentialProvider provider,
        IServiceConnection? serviceConnection,
        CancellationToken cancellationToken,
        string scope = AzureScopes.Default)
    {
        TokenCredential credential = provider.GetCredential(serviceConnection);
        TokenRequestContext requestContext = new TokenRequestContext([scope]);
        ValueTask<AccessToken> token = credential.GetTokenAsync(requestContext, cancellationToken);
        return token;
    }

    public static AccessToken GetToken(
        this IAzureTokenCredentialProvider provider,
        IServiceConnection? serviceConnection,
        string scope = AzureScopes.Default)
    {
        TokenCredential credential = provider.GetCredential(serviceConnection);
        TokenRequestContext requestContext = new TokenRequestContext([scope]);
        AccessToken token = credential.GetToken(requestContext, CancellationToken.None);
        return token;
    }
}
