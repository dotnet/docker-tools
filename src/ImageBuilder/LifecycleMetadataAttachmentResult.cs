// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// The outcome of attaching lifecycle metadata to an image.
/// </summary>
public abstract record LifecycleMetadataAttachmentResult
{
    private LifecycleMetadataAttachmentResult()
    {
    }

    /// <summary>
    /// New lifecycle metadata was attached to the image.
    /// </summary>
    public sealed record Attached(LifecycleArtifact Artifact) : LifecycleMetadataAttachmentResult;

    /// <summary>
    /// The image already has lifecycle metadata with the requested EOL date.
    /// </summary>
    public sealed record AlreadyMatching(LifecycleArtifact ExistingArtifact) : LifecycleMetadataAttachmentResult;

    /// <summary>
    /// The image has lifecycle metadata with a different EOL date, so it was skipped.
    /// </summary>
    public sealed record ConflictSkipped(LifecycleArtifact ExistingArtifact) : LifecycleMetadataAttachmentResult;
}