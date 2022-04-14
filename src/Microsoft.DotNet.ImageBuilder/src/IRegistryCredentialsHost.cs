// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IRegistryCredentialsHost
{
    IDictionary<string, RegistryCredentials> Credentials { get; }
}
#nullable disable
