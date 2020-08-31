// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class ManifestToolServiceHelper
    {
        public static JArray CreateTagManifest(string mediaType, string digest)
        {
            return new JArray(
                new JObject
                {
                    { "MediaType", mediaType },
                    { "Digest", digest }
                });
        }
    }
}
