// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    /// <summary>
    /// Caches state returned from Docker commands.
    /// </summary>
    internal class DockerServiceCache : IDockerService
    {
        private readonly IDockerService _inner;
        private readonly ConcurrentDictionary<string, DateTime> _createdDateCache = new();
        private readonly ConcurrentDictionary<string, long> _imageSizeCache = new();
        private readonly ConcurrentDictionary<string, bool> _localImageExistsCache = new();
        private readonly ConcurrentDictionary<string, bool> _pulledImages = new();
        private readonly ConcurrentDictionary<string, (Architecture, string?)> _architectureCache = new();

        public DockerServiceCache(IDockerService inner)
        {
            _inner = inner;
        }

        public Architecture Architecture => _inner.Architecture;

        public string? BuildImage(
            string dockerfilePath,
            string buildContextPath,
            string platform,
            IEnumerable<string> tags,
            IDictionary<string, string?> buildArgs,
            IReadOnlyDictionary<string, string> buildSecrets,
            BuildSecretMode buildSecretMode,
            IEnumerable<string> dockerBuildOptions,
            bool isRetryEnabled,
            bool isDryRun,
            CancellationToken cancellationToken) =>
                _inner.BuildImage(
                    dockerfilePath,
                    buildContextPath,
                    platform,
                    tags,
                    buildArgs,
                    buildSecrets,
                    buildSecretMode,
                    dockerBuildOptions,
                    isRetryEnabled,
                    isDryRun,
                    cancellationToken);

        public (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun, CancellationToken cancellationToken) =>
            _architectureCache.GetOrAdd(image, _ =>_inner.GetImageArch(image, isDryRun, cancellationToken));

        public void CreateTag(string image, string tag, bool isDryRun, CancellationToken cancellationToken) =>
            _inner.CreateTag(image, tag, isDryRun, cancellationToken);

        public void CreateManifestList(string manifestListTag, IEnumerable<string> images, bool isDryRun, CancellationToken cancellationToken) =>
            _inner.CreateManifestList(manifestListTag, images, isDryRun, cancellationToken);

        public DateTime GetCreatedDate(string image, bool isDryRun, CancellationToken cancellationToken) =>
            _createdDateCache.GetOrAdd(image, _ => _inner.GetCreatedDate(image, isDryRun, cancellationToken));

        public long GetImageSize(string image, bool isDryRun, CancellationToken cancellationToken) =>
            _imageSizeCache.GetOrAdd(image, _ => _inner.GetImageSize(image, isDryRun, cancellationToken));

        public bool LocalImageExists(string tag, bool isDryRun, CancellationToken cancellationToken) =>
            _localImageExistsCache.GetOrAdd(tag, _ => _inner.LocalImageExists(tag, isDryRun, cancellationToken));

        public void PullImage(string image, string? platform, bool isDryRun, CancellationToken cancellationToken)
        {
            _pulledImages.GetOrAdd(image, _ =>
            {
                _inner.PullImage(image, platform, isDryRun, cancellationToken);
                return true;
            });
        }

        public void PushImage(string tag, bool isDryRun, CancellationToken cancellationToken) =>
            _inner.PushImage(tag, isDryRun, cancellationToken);

        public void PushManifestList(string tag, bool isDryRun, CancellationToken cancellationToken) =>
            _inner.PushManifestList(tag, isDryRun, cancellationToken);
    }
}
