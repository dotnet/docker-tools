// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Annotation keys and accessors for <see cref="LifecycleArtifact"/> metadata.
/// </summary>
public static class LifecycleAnnotations
{
    /// <summary>
    /// Annotation key for the image's end-of-life date.
    /// </summary>
    public const string EndOfLife = "vnd.microsoft.artifact.lifecycle.end-of-life.date";

    /// <summary>
    /// Format used when writing <see cref="EndOfLife"/> values.
    /// </summary>
    public const string EndOfLifeDateFormat = "yyyy-MM-dd";

    extension(LifecycleArtifact artifact)
    {
        /// <summary>
        /// The image's end-of-life date, or null if the annotation is missing or invalid.
        /// </summary>
        /// <remarks>
        /// ImageBuilder writes <see cref="EndOfLifeDateFormat"/>, but older or externally created
        /// artifacts may contain a full timestamp, so parsing is lenient.
        /// </remarks>
        public DateOnly? EndOfLifeDate =>
            artifact.Referrer.Annotations?.TryGetValue(EndOfLife, out string? value) == true
            && DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset endOfLife)
                ? DateOnly.FromDateTime(endOfLife.UtcDateTime)
                : null;

        /// <summary>
        /// Whether the image is past its end-of-life date by more than <paramref name="gracePeriod"/>.
        /// Returns false when there is no end-of-life date.
        /// </summary>
        public bool IsEndOfLife(DateTimeOffset now, TimeSpan gracePeriod) =>
            artifact.EndOfLifeDate is DateOnly endOfLifeDate
            && new DateTimeOffset(endOfLifeDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) + gracePeriod < now;
    }
}
