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

        public Task<string?> GetLocalImageDigestAsync(string tag, bool isDryRun, CancellationToken cancellationToken) =>
            LockHelper.DoubleCheckedLockLookupAsync(_localDigestCacheLock, _localDigestCache, tag,
                ct => _inner.Value.GetLocalImageDigestAsync(tag, isDryRun, ct),
                // Don't allow null digests to be cached. A locally built image won't have a digest until
                // it is pushed so if its digest is retrieved before pushing, we don't want that
                // null to be cached.
                cancellationToken: cancellationToken,
                addToDictionary: val => !string.IsNullOrEmpty(val));

        public Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun, CancellationToken cancellationToken) =>
            LockHelper.DoubleCheckedLockLookupAsync(_manifestDigestCacheLock, _manifestDigestCache, tag,
                ct => _inner.Value.GetManifestDigestShaAsync(tag, isDryRun, ct),
                cancellationToken: cancellationToken,
                addToDictionary: null);
    }
}
