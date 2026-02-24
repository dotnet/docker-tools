// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Configuration;

/// <summary>
/// Configuration for container image signing via ESRP.
/// </summary>
public sealed record SigningConfiguration
{
    /// <summary>
    /// Whether signing is enabled. Set via pipeline variable.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Certificate ID used by DDSignFiles.dll for signing container images.
    /// </summary>
    public int ImageSigningKeyCode { get; set; }

    /// <summary>
    /// Certificate ID used by DDSignFiles.dll for signing referrer artifacts (SBOMs, etc.).
    /// </summary>
    public int ReferrerSigningKeyCode { get; set; }

    /// <summary>
    /// The sign type to use with DDSignFiles.dll. Use "test" for non-production and "real" for production.
    /// </summary>
    public string SignType { get; set; } = "test";

    /// <summary>
    /// Name of the notation trust store to use for signature verification (e.g., "supplychain" or "test").
    /// </summary>
    public string TrustStoreName { get; set; } = "test";
}
