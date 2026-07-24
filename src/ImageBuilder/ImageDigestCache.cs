// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    public class ImageDigestCache(Lazy<IManifestService> manifestService)
    {
        private readonly Lazy<IManifestService> _inner = manifestService;
        private readonly Dictionary<string, string?> _localDigestCache = [];
        private readonly Dictionary<string, string> _manifestDigestCache = [];
        private readonly SemaphoreSlim _localDigestCacheLock = new(1);
        private readonly SemaphoreSlim _manifestDigestCacheLock = new(1);

        /// <summary>
        /// Records a known digest for a local tag without querying a registry.
        /// </summary>
        /// <remarks>
        /// This supports images produced or copied during the current build before their tags are
        /// available from the registry.
        /// </remarks>
        public void AddDigest(string tag, string digest)
        {
            _localDigestCacheLock.Wait();
            try
            {
                _localDigestCache[tag] = digest;
            }
            finally
            {
                _localDigestCacheLock.Release();
            }
        }

        /// <summary>
        /// Gets and caches a digest known to identify a locally available image.
        /// </summary>
        /// <remarks>
        /// On a cache miss, the lookup verifies that the registry tag's current digest is present
        /// among the local image's repository digests. Null results are not cached because an image
        /// built locally may acquire a repository digest after it is pushed. A digest supplied
        /// through <see cref="AddDigest"/> is already trusted and does not require registry validation.
        /// </remarks>
        public Task<string?> GetLocalImageDigestAsync(string tag, bool isDryRun) =>
            LockHelper.DoubleCheckedLockLookupAsync(_localDigestCacheLock, _localDigestCache, tag,
                () => _inner.Value.GetLocalImageDigestAsync(tag, isDryRun),
                // Don't allow null digests to be cached. A locally built image won't have a digest until
                // it is pushed so if its digest is retrieved before pushing, we don't want that
                // null to be cached.
                val => !string.IsNullOrEmpty(val));

        public Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun) =>
            LockHelper.DoubleCheckedLockLookupAsync(_manifestDigestCacheLock, _manifestDigestCache, tag,
                () => _inner.Value.GetManifestDigestShaAsync(tag, isDryRun));
    }
}
