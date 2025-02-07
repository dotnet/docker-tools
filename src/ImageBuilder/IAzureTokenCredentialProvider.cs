// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Core;

namespace Microsoft.DotNet.DockerTools.ImageBuilder;

#nullable enable
public interface IAzureTokenCredentialProvider
{
    TokenCredential GetCredential(string scope = AzureScopes.DefaultAzureManagementScope);
}
