// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
/// <summary>
/// Contains data related to authenticating to one or more registries.
/// </summary>
/// <param name="OwnedAcr">Name of the ACR owned for the product.</param>
/// <param name="Credentials">A mapping of registry names to credentials.</param>
public record RegistryAuthContext(string? OwnedAcr, IDictionary<string, RegistryCredentials> Credentials);
