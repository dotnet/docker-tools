// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Well-known OCI annotation keys.
/// </summary>
/// <remarks>
/// See https://github.com/opencontainers/image-spec/blob/v1.1.1/annotations.md#pre-defined-annotation-keys
/// </remarks>
public static class OciAnnotations
{
    /// <summary>
    /// Date and time the artifact was created, in RFC 3339 format. ORAS sets this automatically
    /// when packing a manifest unless the caller provides it.
    /// </summary>
    public const string ImageCreated = "org.opencontainers.image.created";

    extension(ReferrerInfo referrer)
    {
        /// <summary>
        /// When the referrer was created according to its <see cref="ImageCreated"/> annotation,
        /// or null if the annotation is missing or invalid.
        /// </summary>
        public DateTimeOffset? Created
        {
            get
            {
                if (referrer.Annotations is null)
                {
                    return null;
                }

                if (!referrer.Annotations.TryGetValue(ImageCreated, out string? value))
                {
                    return null;
                }

                if (!DateTimeOffset.TryParse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset created))
                {
                    return null;
                }

                return created;
            }
        }
    }
}
