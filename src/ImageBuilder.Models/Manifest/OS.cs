// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

/// <summary>
/// The generic operating system family of an image. Serialized to the manifest as a lowercase
/// string (e.g. "linux", "windows"). The specific OS version is captured separately by
/// <see cref="Platform.OsVersion"/> (e.g. "azurelinux3.0", "windowsservercore-ltsc2025").
/// </summary>
public enum OS
{
    Linux,
    Windows,
}
