// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ManifestToolServiceExtensions
    {
        public static string GetManifestDigestSha(
            this IManifestToolService manifestToolService, ManifestMediaType mediaType, string tag, bool isDryRun)
        {
            IEnumerable<JObject> tagManifests = manifestToolService.Inspect(tag, isDryRun).OfType<JObject>();
            string digest;

            bool hasSupportedMediaType = false;

            if (hasSupportedMediaType |= mediaType.HasFlag(ManifestMediaType.ManifestList))
            {
                digest = GetDigestOfMediaType(
                    tag, tagManifests, ManifestToolService.ManifestListMediaType, throwIfNull: mediaType == ManifestMediaType.ManifestList);

                if (digest != null)
                {
                    return digest;
                }
            }
            
            if (hasSupportedMediaType |= mediaType.HasFlag(ManifestMediaType.Manifest))
            {
                return GetDigestOfMediaType(
                    tag, tagManifests, ManifestToolService.ManifestMediaType, throwIfNull: true);
            }

            if (!hasSupportedMediaType)
            {
                throw new ArgumentException($"Unsupported media type: '{mediaType}'.", nameof(mediaType));
            }

            return null;
        }

        private static string GetDigestOfMediaType(string tag, IEnumerable<JObject> tagManifests, string mediaType, bool throwIfNull)
        {
            string digest = tagManifests?
                .FirstOrDefault(manifestType => manifestType["MediaType"].Value<string>() == mediaType)
                ?["Digest"].Value<string>();
            if (digest is null)
            {
                if (throwIfNull)
                {
                    throw new InvalidOperationException($"Unable to find digest for tag '{tag}' with media type '{mediaType}'.");
                }
                return null;
            }

            return digest;
        }
    }
}
