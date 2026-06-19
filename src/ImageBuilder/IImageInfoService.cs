// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Publishes image-info content as OCI artifacts.
/// </summary>
public interface IImageInfoService
{
    /// <summary>
    /// Pushes the given image-info content as an OCI artifact to the given registry, using the
    /// repository and tags declared by the manifest's <c>imageInfo</c> object.
    /// </summary>
    /// <param name="manifest">The manifest declaring the image-info artifact repository and tags.</param>
    /// <param name="imageInfoContent">The image-info content to push.</param>
    /// <param name="registry">The registry to push the artifact to (e.g. the publish registry).</param>
    /// <param name="repoPrefix">A prefix to prepend to the artifact's repository name.</param>
    /// <param name="isDryRun">When <see langword="true"/>, no artifact is pushed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// When the manifest does not declare an <c>imageInfo</c> object, the push is skipped and a
    /// warning is logged rather than throwing, so that publishing is not blocked.
    /// </remarks>
    Task PushImageInfoArtifactAsync(
        ManifestInfo manifest,
        byte[] imageInfoContent,
        string registry,
        string? repoPrefix,
        bool isDryRun,
        CancellationToken cancellationToken = default);
}
