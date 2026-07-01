// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Custom (non-OCI) image label keys applied to built images.
/// </summary>
public static class ImageBuilderLabels
{
    /// <summary>
    /// Path of the Dockerfile the image was built from, relative to the root of the source repository.
    /// </summary>
    public const string Dockerfile = "com.microsoft.imagebuilder.dockerfile";
}
