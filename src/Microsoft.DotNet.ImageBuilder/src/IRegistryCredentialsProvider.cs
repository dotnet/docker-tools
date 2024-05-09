// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IRegistryCredentialsProvider
{
    ValueTask<RegistryCredentials?> GetCredentialsAsync(string registry, string? ownedAcr, IRegistryCredentialsHost? credsHost);
}
