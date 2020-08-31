// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    /// <summary>
    /// Caches state returned from Docker commands.
    /// </summary>
    internal class DockerServiceCache : IDockerService
    {
        private readonly IDockerService inner;
        private readonly ConcurrentDictionary<string, DateTime> createdDateCache = new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, string> imageDigestCache = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, long> imageSizeCache = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, bool> localImageExistsCache = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, bool> pulledImages = new ConcurrentDictionary<string, bool>();

        public DockerServiceCache(IDockerService inner)
        {
            this.inner = inner;
        }

        public Architecture Architecture => inner.Architecture;

        public string BuildImage(string dockerfilePath, string buildContextPath, IEnumerable<string> tags, IDictionary<string, string> buildArgs, bool isRetryEnabled, bool isDryRun) =>
            inner.BuildImage(dockerfilePath, buildContextPath, tags, buildArgs, isRetryEnabled, isDryRun);

        public void CreateTag(string image, string tag, bool isDryRun) =>
            inner.CreateTag(image, tag, isDryRun);

        public DateTime GetCreatedDate(string image, bool isDryRun) =>
            createdDateCache.GetOrAdd(image, _ => inner.GetCreatedDate(image, isDryRun));

        public string GetImageDigest(string image, bool isDryRun) =>
            imageDigestCache.GetOrAdd(image, _ => inner.GetImageDigest(image, isDryRun));

        public long GetImageSize(string image, bool isDryRun) =>
            imageSizeCache.GetOrAdd(image, _ => inner.GetImageSize(image, isDryRun));
        
        public bool LocalImageExists(string tag, bool isDryRun) =>
            localImageExistsCache.GetOrAdd(tag, _ => inner.LocalImageExists(tag, isDryRun));
        
        public void PullImage(string image, bool isDryRun)
        {
            pulledImages.GetOrAdd(image, _ =>
            {
                inner.PullImage(image, isDryRun);
                return true;
            });
        }

        public void PushImage(string tag, bool isDryRun) =>
            inner.PushImage(tag, isDryRun);
    }
}
