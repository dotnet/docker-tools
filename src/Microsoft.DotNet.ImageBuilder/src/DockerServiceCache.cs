// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    /// <summary>
    /// Caches state returned from Docker commands.
    /// </summary>
    internal class DockerServiceCache : IDockerService
    {
        private readonly IDockerService _inner;
        private readonly ConcurrentDictionary<string, DateTime> _createdDateCache = new();
        private readonly ImageDigestCache _imageDigestCache;
        private readonly Dictionary<string, IEnumerable<string>> _imageLayersCache = new();
        private readonly SemaphoreSlim _imageLayersCacheLock = new(1);
        private readonly ConcurrentDictionary<string, long> _imageSizeCache = new();
        private readonly ConcurrentDictionary<string, bool> _localImageExistsCache = new();
        private readonly ConcurrentDictionary<string, bool> _pulledImages = new();
        private readonly ConcurrentDictionary<string, (Architecture, string?)> _architectureCache = new();

        public DockerServiceCache(IDockerService inner)
        {
            _inner = inner;
            _imageDigestCache = new ImageDigestCache(inner);
        }

        public Architecture Architecture => _inner.Architecture;

        public string? BuildImage(
            string dockerfilePath, string buildContextPath, string platform, IEnumerable<string> tags,
            IDictionary<string, string?> buildArgs, IEnumerable<(string Id, string Src)> secretInfos, bool isRetryEnabled, bool isDryRun) =>
            _inner.BuildImage(dockerfilePath, buildContextPath, platform, tags, buildArgs, secretInfos, isRetryEnabled, isDryRun);

        public (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun) =>
            _architectureCache.GetOrAdd(image, _ =>_inner.GetImageArch(image, isDryRun));

        public void CreateTag(string image, string tag, bool isDryRun) =>
            _inner.CreateTag(image, tag, isDryRun);

        public void CreateManifestList(string manifestListTag, IEnumerable<string> images, bool isDryRun) =>
            _inner.CreateManifestList(manifestListTag, images, isDryRun);

        public DateTime GetCreatedDate(string image, bool isDryRun) =>
            _createdDateCache.GetOrAdd(image, _ => _inner.GetCreatedDate(image, isDryRun));

        public Task<string?> GetImageDigestAsync(string image, IRegistryCredentialsHost credsHost, bool isDryRun) =>
            _imageDigestCache.GetImageDigestAsync(image, credsHost, isDryRun);

        public Task<IEnumerable<string>> GetImageManifestLayersAsync(string image, IRegistryCredentialsHost credsHost, bool isDryRun) =>
            LockHelper.DoubleCheckedLockLookupAsync(_imageLayersCacheLock, _imageLayersCache, image,
                () => _inner.GetImageManifestLayersAsync(image, credsHost, isDryRun));

        public long GetImageSize(string image, bool isDryRun) =>
            _imageSizeCache.GetOrAdd(image, _ => _inner.GetImageSize(image, isDryRun));

        public bool LocalImageExists(string tag, bool isDryRun) =>
            _localImageExistsCache.GetOrAdd(tag, _ => _inner.LocalImageExists(tag, isDryRun));

        public void PullImage(string image, string? platform, bool isDryRun)
        {
            _pulledImages.GetOrAdd(image, _ =>
            {
                _inner.PullImage(image, platform, isDryRun);
                return true;
            });
        }

        public void PushImage(string tag, bool isDryRun) =>
            _inner.PushImage(tag, isDryRun);

        public void PushManifestList(string tag, bool isDryRun) =>
            _inner.PushManifestList(tag, isDryRun);
    }
}
#nullable disable
