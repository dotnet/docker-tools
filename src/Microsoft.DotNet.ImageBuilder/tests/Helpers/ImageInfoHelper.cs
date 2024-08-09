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

        public static (ImageArtifactDetails, string) CreateImageInfoOnDisk(
            TempFolderContext tempFolderContext,
            IEnumerable<string> repos,
            IEnumerable<string> oses,
            IEnumerable<string> archs,
            IEnumerable<string> versions)
        {
            ImageArtifactDetails imageInfo = CreateImageInfo(repos, oses, archs, versions);
            string filePath = WriteImageInfoToDisk(imageInfo, directory: tempFolderContext.Path);
            return (imageInfo, filePath);
        }

        public static ImageArtifactDetails CreateImageInfo(
            IEnumerable<string> repos,
            IEnumerable<string> oses,
            IEnumerable<string> archs,
            IEnumerable<string> versions)
        {
            IEnumerable<RepoData> repoDatas =
                from repoName in repos
                select CreateRepo(repoName, oses, archs, versions);

            return new ImageArtifactDetails()
            {
                Repos = repoDatas.ToList()
            };
        }

        private static RepoData CreateRepo(
            string repoName,
            IEnumerable<string> oses,
            IEnumerable<string> archs,
            IEnumerable<string> versions)
        {
            IEnumerable<ImageData> imageDatas = 
                from version in versions
                from os in oses
                select CreateImage(repoName, version, os, archs);
            
            return new RepoData
            {
                Repo = repoName,
                Images = imageDatas.ToList()
            };
        }

        private static ImageData CreateImage(
            string repoName,
            string productVersion,
            string os,
            IEnumerable<string> archs)
        {
            IEnumerable<PlatformData> platformDatas =
                from arch in archs
                select CreatePlatformSimple(repoName, productVersion, os, arch);

            return new ImageData
            {
                Platforms = platformDatas.ToList(),
                ProductVersion = productVersion,
                Manifest = new ManifestData
                {
                    SharedTags = GetSharedTags(productVersion, os),
                    Digest = CalculateDigest(repoName, productVersion, os),
                }
            };
        }

        private static PlatformData CreatePlatformSimple(
            string repoName,
            string productVersion,
            string os,
            string arch)
        {
            return CreatePlatform(
                dockerfile: string.Join('/', [repoName, productVersion, os, arch, "Dockerfile"]),
                digest: CalculateDigest(repoName, productVersion, os, arch),
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
            string osVersion = "focal",
            List<string> simpleTags = null,
            string baseImageDigest = null,
            DateTime? created = null,
            List<string> layers = null)
        {
            if (digest is null)
            {
                digest = $"sha256:{new string(Enumerable.Repeat('0', 64).ToArray())}";
            }

            PlatformData platform = new()
            {
                Dockerfile = dockerfile,
                Digest = digest,
                Architecture = architecture,
                OsType = osType,
                OsVersion = osVersion,
                SimpleTags = simpleTags ?? new List<string>(),
                Layers = layers ?? new List<string>(),
                BaseImageDigest = baseImageDigest,
            };

            if (created.HasValue)
            {
                platform.Created = created.Value;
            }

            return platform;
        }

        private static List<string> GetSharedTags(string productVersion, string os)
        {
            return
            [
                productVersion,
                $"{productVersion}-{os}"
            ];
        }

        private static string CalculateDigest(string repoName, string version, string os, string arch = "")
        {
            string uniqueIdentifier = repoName + version + os + arch;
            return "sha256:" + CalculateSHA256(uniqueIdentifier).ToLowerInvariant();
        }

        private static string CalculateSHA256(string s) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    }
}
