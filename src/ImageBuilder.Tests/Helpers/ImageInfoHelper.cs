#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class ImageInfoHelper
    {
        public static string WriteImageInfoToDisk(ImageArtifactDetails imageInfo, string directory)
        {
            string imageInfoJson = JsonConvert.SerializeObject(imageInfo);
            string imageInfoPath = Path.Combine(directory, "image-info.json");
            File.WriteAllText(imageInfoPath, imageInfoJson);
            return imageInfoPath;
        }

        public static void CompareImageArtifactDetails(ImageArtifactDetails expected, ImageArtifactDetails actual)
        {
            Assert.Equal(JsonHelper.SerializeObject(expected), JsonHelper.SerializeObject(actual));
        }

        public static ImageArtifactDetails CreateImageInfo(
            string registry,
            IEnumerable<string> repos,
            IEnumerable<string> oses,
            IEnumerable<string> archs,
            IEnumerable<string> versions)
        {
            IEnumerable<RepoData> repoDatas =
                from repoName in repos
                select CreateRepo(registry, repoName, oses, archs, versions);

            return new ImageArtifactDetails()
            {
                Repos = repoDatas.ToList()
            };
        }

        public static ImageArtifactDetails CreateImageArtifactDetails(params List<RepoData> repos) =>
            new()
            {
                Repos = repos,
            };

        private static RepoData CreateRepo(
            string registry,
            string repoName,
            IEnumerable<string> oses,
            IEnumerable<string> archs,
            IEnumerable<string> versions)
        {
            IEnumerable<ImageData> imageDatas =
                from version in versions
                from os in oses
                select CreateImage(registry, repoName, version, os, archs);

            return new RepoData
            {
                Repo = repoName,
                Images = imageDatas.ToList()
            };
        }

        public static RepoData CreateRepoData(string name, params List<ImageData> imageDatas) =>
            new()
            {
                Repo = name,
                Images = imageDatas,
            };

        private static ImageData CreateImage(
            string registry,
            string repoName,
            string productVersion,
            string os,
            IEnumerable<string> archs)
        {
            IEnumerable<PlatformData> platformDatas =
                from arch in archs
                select CreatePlatformSimple(registry, repoName, productVersion, os, arch);

            return new ImageData
            {
                Platforms = platformDatas.ToList(),
                ProductVersion = productVersion,
                Manifest = new ManifestData
                {
                    SharedTags = GetSharedTags(productVersion, os),
                    Digest = GenerateFakeDigest(registry, repoName, productVersion, os),
                }
            };
        }

        public static ImageData CreateImageData(params List<PlatformData> platforms) =>
            new()
            {
                Platforms = platforms
            };

        public static ImageData CreateImageData(List<string> sharedTags, params List<PlatformData> platforms) =>
            new()
            {
                Platforms = platforms,
                Manifest = new ManifestData
                {
                    SharedTags = sharedTags,
                }
            };

        private static PlatformData CreatePlatformSimple(
            string registry,
            string repoName,
            string productVersion,
            string os,
            string arch)
        {
            return CreatePlatform(
                // Workaround for ambiguous method call, will be fixed with https://github.com/dotnet/csharplang/issues/8374
                dockerfile: string.Join('/', new string[] { repoName, productVersion, os, arch, "Dockerfile" }),
                digest: GenerateFakeDigest(registry, repoName, productVersion, os, arch),
                architecture: arch,
                osVersion: os,
                simpleTags: [ $"{productVersion}-{os}-{arch}" ]
            );
        }

        public static PlatformData CreatePlatform(
            string dockerfile,
            string digest = null,
            string architecture = "amd64",
            string osType = "Linux",
            string osVersion = "noble",
            List<string> simpleTags = null,
            string baseImageDigest = null,
            DateTime? created = null,
            List<Layer> layers = null,
            bool isUnchanged = false,
            string commitUrl = "")
        {
            digest ??= $"sha256:{new string(Enumerable.Repeat('0', 64).ToArray())}";

            PlatformData platform = new()
            {
                Dockerfile = dockerfile,
                Digest = digest,
                Architecture = architecture,
                OsType = osType,
                OsVersion = osVersion,
                SimpleTags = simpleTags ?? [],
                Layers = layers ?? [],
                BaseImageDigest = baseImageDigest,
                IsUnchanged = isUnchanged,
                CommitUrl = commitUrl
            };

            if (created.HasValue)
            {
                platform.Created = created.Value;
            }

            return platform;
        }

        private static List<string> GetSharedTags(string productVersion, string os) =>
            [ productVersion, $"{productVersion}-{os}" ];

        private static string GenerateFakeDigest(
            string registry,
            string repoName,
            string version,
            string os,
            string arch = "")
        {
            string repoPrefix = $"{registry}/{repoName}";
            string uniqueIdentifier = registry + repoName + version + os + arch;
            string digest = "sha256:" + CalculateSHA256(uniqueIdentifier).ToLowerInvariant();
            return repoPrefix + "@" + digest;
        }

        private static string CalculateSHA256(string s) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    }
}
