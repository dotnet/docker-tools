// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace TemplateTool;

/// <summary>
/// An image with the repo it belongs to.
/// </summary>
/// <param name="Key">The image's repo, group (if any) and image keys, like "sdk__10.0__noble".</param>
/// <param name="Repo">The repo the image belongs to.</param>
/// <param name="Image">The image.</param>
internal sealed record ImageEntry(string Key, Repo Repo, Image Image);
