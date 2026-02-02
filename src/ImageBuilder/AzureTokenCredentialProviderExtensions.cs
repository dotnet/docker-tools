// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

internal static class AzureTokenCredentialProviderExtensions
{
    public static ValueTask<AccessToken> GetTokenAsync(
        this IAzureTokenCredentialProvider provider,
        IServiceConnection? serviceConnection,
        string scope = AzureScopes.Default)
    {
        var credential = provider.GetCredential(serviceConnection);
        var requestContext = new TokenRequestContext([scope]);
        var token = credential.GetTokenAsync(requestContext, CancellationToken.None);
        return token;
    }

    public static AccessToken GetToken(
        this IAzureTokenCredentialProvider provider,
        IServiceConnection? serviceConnection,
        string scope = AzureScopes.Default)
    {
        var credential = provider.GetCredential(serviceConnection);
        var requestContext = new TokenRequestContext([scope]);
        var token = credential.GetToken(requestContext, CancellationToken.None);
        return token;
    }
}
