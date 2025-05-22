// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal static class AzureTokenCredentialProviderExtensions
{
    public static ValueTask<AccessToken> GetTokenAsync(
        this IAzureTokenCredentialProvider provider,
        IServiceConnection serviceConnection,
        string scope = AzureScopes.DefaultAzureManagementScope)
    {
        return provider
            .GetCredential(serviceConnection, scope)
            .GetTokenAsync(new TokenRequestContext([scope]), CancellationToken.None);
    }
}
