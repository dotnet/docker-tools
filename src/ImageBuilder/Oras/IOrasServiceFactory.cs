// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Creates <see cref="IOrasService"/> instances scoped to a specific set of registry credentials.
/// </summary>
/// <remarks>
/// Use this factory when the credentials available for registry access depend on
/// runtime context (e.g., command-line credentials options). For singleton scenarios
/// where no per-call credentials host is needed, depend on <see cref="IOrasService"/> directly.
/// </remarks>
public interface IOrasServiceFactory
{
    /// <summary>
    /// Creates an <see cref="IOrasService"/> that resolves registry credentials using the
    /// supplied <paramref name="credsHost"/>.
    /// </summary>
    /// <param name="credsHost">
    /// Optional host providing explicit per-registry credentials. When null, only credentials
    /// from the publish configuration (e.g., service connections) are available.
    /// </param>
    IOrasService Create(IRegistryCredentialsHost? credsHost = null);
}
