// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public class ImageDigestCache(Lazy<IManifestService> manifestService)
    {
        private readonly Lazy<IManifestService> _inner = manifestService;
        private readonly Dictionary<string, string?> _digestCache = new();
        private readonly SemaphoreSlim _digestCacheLock = new(1);

        public void AddDigest(string tag, string digest)
        {
            _digestCacheLock.Wait();
            try
            {
                _digestCache[tag] = digest;
            }
            finally
            {
                _digestCacheLock.Release();
            }
        }

        public Task<string?> GetLocalImageDigestAsync(string tag, bool isDryRun) =>
            LockHelper.DoubleCheckedLockLookupAsync(_digestCacheLock, _digestCache, tag,
                () => _inner.Value.GetLocalImageDigestAsync(tag, isDryRun),
                // Don't allow null digests to be cached. A locally built image won't have a digest until
                // it is pushed so if its digest is retrieved before pushing, we don't want that 
                // null to be cached.
                val => !string.IsNullOrEmpty(val));
    }
}
#nullable disable
