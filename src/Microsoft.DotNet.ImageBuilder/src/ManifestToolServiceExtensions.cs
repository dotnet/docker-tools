// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ManifestToolServiceExtensions
    {
        public static string GetImageDigest(this IManifestToolService manifestToolService, string tag, bool isDryRun)
        {
            JArray tagManifest = manifestToolService.Inspect(tag, isDryRun);
            return tagManifest?
                .OfType<JObject>()
                .First(manifestType =>
                {
                    string mediaType = manifestType["MediaType"].Value<string>();
                    return mediaType == ManifestToolService.ManifestListMediaType ||
                        mediaType == ManifestToolService.ManifestMediaType;
                })
                ["Digest"].Value<string>();
        }
    }
}
