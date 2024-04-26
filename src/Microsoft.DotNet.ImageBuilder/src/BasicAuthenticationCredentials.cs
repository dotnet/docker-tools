// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public class BasicAuthenticationCredentials(string userName, string password)
{
    public string UserName { get; } = userName;
    public string Password { get; } = password;
}
#nullable disable
