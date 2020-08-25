// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        private readonly Dictionary<string, DateTime> createdDateCache = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, string> imageDigestCache = new Dictionary<string, string>();
        private readonly Dictionary<string, long> imageSizeCache = new Dictionary<string, long>();
        private readonly Dictionary<string, bool> localImageExistsCache = new Dictionary<string, bool>();
        private readonly HashSet<string> pulledImages = new HashSet<string>();

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
            GetCachedValue(image, createdDateCache, () => inner.GetCreatedDate(image, isDryRun));

        public string GetImageDigest(string image, bool isDryRun) =>
            GetCachedValue(image, imageDigestCache, () => inner.GetImageDigest(image, isDryRun));

        public long GetImageSize(string image, bool isDryRun) =>
            GetCachedValue(image, imageSizeCache, () => inner.GetImageSize(image, isDryRun));
        
        public bool LocalImageExists(string tag, bool isDryRun) =>
            GetCachedValue(tag, localImageExistsCache, () => inner.LocalImageExists(tag, isDryRun));
        
        public void PullImage(string image, bool isDryRun)
        {
            if (!pulledImages.Contains(image))
            {
                inner.PullImage(image, isDryRun);
                pulledImages.Add(image);
            }
        }

        public void PushImage(string tag, bool isDryRun) =>
            inner.PushImage(tag, isDryRun);

        private static TValue GetCachedValue<TKey, TValue>(TKey key, Dictionary<TKey, TValue> cache, Func<TValue> getValue)
        {
            if (!cache.TryGetValue(key, out TValue value))
            {
                value = getValue();
                cache.Add(key, value);
            }

            return value;
        }
    }
}
