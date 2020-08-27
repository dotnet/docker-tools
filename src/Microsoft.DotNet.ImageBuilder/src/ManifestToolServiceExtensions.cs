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
        public static ManifestDigest GetManifestListDigest(this IManifestToolService manifestToolService, string tag, bool isDryRun)
        {
            IEnumerable<JObject> tagManifests = manifestToolService.Inspect(tag, isDryRun).OfType<JObject>();
            return GetDigestOfMediaType(tag, tagManifests, ManifestToolService.ManifestListMediaType, throwIfNull: true);
        }

        public static ManifestDigest GetAnyManifestDigest(this IManifestToolService manifestToolService, string tag, bool isDryRun)
        {
            IEnumerable<JObject> tagManifests = manifestToolService.Inspect(tag, isDryRun).OfType<JObject>();
            ManifestDigest digest = GetDigestOfMediaType(tag, tagManifests, ManifestToolService.ManifestListMediaType, throwIfNull: false);
            if (digest is null)
            {
                digest = GetDigestOfMediaType(tag, tagManifests, ManifestToolService.ManifestMediaType, throwIfNull: true);
            }

            return digest;
        }

        private static ManifestDigest GetDigestOfMediaType(string tag, IEnumerable<JObject> tagManifests, string mediaType, bool throwIfNull)
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

            return new ManifestDigest(digest, mediaType);
        }
    }
}
