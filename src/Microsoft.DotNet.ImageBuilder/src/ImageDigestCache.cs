// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public class ImageDigestCache
    {
        private readonly IDockerService _dockerService;
        private readonly Dictionary<string, string?> _digestCache = new();
        private readonly SemaphoreSlim _digestCacheLock = new(1);

        public ImageDigestCache(IDockerService dockerService)
        {
            _dockerService = dockerService;
        }

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

        public Task<string?> GetImageDigestAsync(string tag, RegistryAuthContext registryAuthContext, bool isDryRun) =>
            LockHelper.DoubleCheckedLockLookupAsync(_digestCacheLock, _digestCache, tag,
                () => _dockerService.GetImageDigestAsync(tag, registryAuthContext, isDryRun),
                // Don't allow null digests to be cached. A locally built image won't have a digest until
                // it is pushed so if its digest is retrieved before pushing, we don't want that 
                // null to be cached.
                val => !string.IsNullOrEmpty(val));
    }
}
#nullable disable
