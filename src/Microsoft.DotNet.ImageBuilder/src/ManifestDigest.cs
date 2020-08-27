// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder
{
    public class ManifestDigest
    {
        public ManifestDigest(string digestSha, string mediaType)
        {
            this.DigestSha = digestSha;
            this.MediaType = mediaType;
        }

        public string DigestSha { get; }
        public string MediaType { get; }
    }
}
