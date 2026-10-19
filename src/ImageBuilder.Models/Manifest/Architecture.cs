// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

// Enum values must align with the $GOARCH values specified at https://golang.org/doc/install/source#environment
public enum Architecture
{
    ARM,
    ARM64,
    AMD64,
}
