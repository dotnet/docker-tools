// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<string, IEnumerable<string>> _imageLayersCache = new();
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
            IDictionary<string, string?> buildArgs, bool isRetryEnabled, bool isDryRun) =>
            _inner.BuildImage(dockerfilePath, buildContextPath, platform, tags, buildArgs, isRetryEnabled, isDryRun);

        public (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun) =>
            _architectureCache.GetOrAdd(image, _ =>_inner.GetImageArch(image, isDryRun));

        public void CreateTag(string image, string tag, bool isDryRun) =>
            _inner.CreateTag(image, tag, isDryRun);

        public DateTime GetCreatedDate(string image, bool isDryRun) =>
            _createdDateCache.GetOrAdd(image, _ => _inner.GetCreatedDate(image, isDryRun));

        public string? GetImageDigest(string image, bool isDryRun) =>
            _imageDigestCache.GetImageDigest(image, isDryRun);

        public IEnumerable<string> GetImageManifestLayers(string image, bool isDryRun) =>
            _imageLayersCache.GetOrAdd(image, _ => _inner.GetImageManifestLayers(image, isDryRun));

        public long GetImageSize(string image, bool isDryRun) =>
            _imageSizeCache.GetOrAdd(image, _ => _inner.GetImageSize(image, isDryRun));
        
        public bool LocalImageExists(string tag, bool isDryRun) =>
            _localImageExistsCache.GetOrAdd(tag, _ => _inner.LocalImageExists(tag, isDryRun));
        
        public void PullImage(string image, bool isDryRun)
        {
            _pulledImages.GetOrAdd(image, _ =>
            {
                _inner.PullImage(image, isDryRun);
                return true;
            });
        }

        public void PushImage(string tag, bool isDryRun) =>
            _inner.PushImage(tag, isDryRun);
    }
}
#nullable disable
