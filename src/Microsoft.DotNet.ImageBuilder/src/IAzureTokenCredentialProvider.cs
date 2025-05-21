// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IAzureTokenCredentialProvider
{
    TokenCredential GetCredential(
        ServiceConnectionOptions? serviceConnection,
        string scope = AzureScopes.DefaultAzureManagementScope);
}
