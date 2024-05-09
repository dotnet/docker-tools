// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal static class RegistryCredentialsHostExtensions
{
    public static RegistryCredentials? TryGetCredentials(this IRegistryCredentialsHost credsHost, string registry)
    {
        credsHost.Credentials.TryGetValue(registry, out RegistryCredentials? registryCreds);
        return registryCreds;
    }
}
