// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Notation;

/// <summary>
/// Client for executing Notation CLI commands for signature verification.
/// </summary>
public interface INotationClient
{
    /// <summary>
    /// Verifies the signature of a container image using the notation CLI.
    /// </summary>
    /// <param name="imageReference">Fully-qualified image reference including digest (e.g., "registry.io/repo@sha256:...").</param>
    /// <param name="isDryRun">If true, logs the command without executing.</param>
    string Verify(string imageReference, bool isDryRun);

    /// <summary>
    /// Imports a trust policy JSON file into the notation configuration.
    /// </summary>
    /// <param name="policyPath">Path to the trust policy JSON file.</param>
    void ImportTrustPolicy(string policyPath);

    /// <summary>
    /// Adds a certificate to a notation trust store.
    /// </summary>
    /// <param name="storeType">Trust store type (e.g., "ca" or "tsa").</param>
    /// <param name="storeName">Name of the trust store (e.g., "supplychain").</param>
    /// <param name="certPath">Path to the certificate file.</param>
    void AddCertificate(string storeType, string storeName, string certPath);
}
