// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder
{
    public class ImageDigestCache
    {
        private readonly IDockerService dockerService;
        private readonly Dictionary<string, string> digestCache = new Dictionary<string, string>();

        public ImageDigestCache(IDockerService dockerService)
        {
            this.dockerService = dockerService;
        }

        public void AddDigest(string tag, string digest)
        {
            lock(digestCache)
            {
                digestCache[tag] = digest;
            }
        }

        public string GetImageDigest(string tag, bool isDryRun) =>
            LockHelper.DoubleCheckedLockLookup(digestCache, digestCache, tag,
                () => dockerService.GetImageDigest(tag, isDryRun),
                // Don't allow null digests to be cached. A locally built image won't have a digest until
                // it is pushed so if its digest is retrieved before pushing, we don't want that 
                // null to be cached.
                val => !String.IsNullOrEmpty(val));
    }
}
