// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal static class AzureTokenCredentialProviderExtensions
{
    public static ValueTask<AccessToken> GetTokenAsync(this IAzureTokenCredentialProvider provider, string scope = AuthHelper.DefaultAzureManagementScope) =>
        provider.GetCredential(scope).GetTokenAsync(new TokenRequestContext([scope]), CancellationToken.None);
}
