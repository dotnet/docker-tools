// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class ManifestHelper
    {
        public static Manifest CreateManifest(params Repo[] repos)
        {
            return new Manifest
            {
                Repos = repos
            };
        }

        public static Repo CreateRepo(string name, params Image[] images)
        {
            return CreateRepo(name, images, readme: null);
        }

        public static Repo CreateRepo(
            string name,
            Image[] images,
            string readme = null,
            string readmeTemplate = null,
            string mcrTagsMetadataTemplate = null)
        {
            Readme[] readmes = Array.Empty<Readme>();
            if (readme is not null)
            {
                readmes = new[]
                {
                    new Readme(readme, readmeTemplate)
                };
            }

            return new Repo
            {
                Name = name,
                Id = name,
                Images = images,
                McrTagsMetadataTemplate = mcrTagsMetadataTemplate,
                Readmes = readmes
            };
        }

        public static Image CreateImage(params Platform[] platforms) =>
            CreateImage(platforms, (IDictionary<string, Tag>)null);

        public static Image CreateImage(Platform[] platforms, IDictionary<string, Tag> sharedTags = null, string productVersion = null)
        {
            return new Image
            {
                Platforms = platforms,
                SharedTags = sharedTags,
                ProductVersion = productVersion
            };
        }

        public static Platform CreatePlatform(
            string dockerfilePath,
            string[] tags,
            OS os = OS.Linux,
            string osVersion = "focal",
            Architecture architecture = Architecture.AMD64,
            string variant = null,
            CustomBuildLegGroup[] customBuildLegGroups = null,
            string dockerfileTemplatePath = null)
        {
            return new Platform
            {
                Dockerfile = dockerfilePath,
                DockerfileTemplate = dockerfileTemplatePath,
                OsVersion = osVersion,
                OS = os,
                Tags = tags.ToDictionary(tag => tag, tag => new Tag()),
                Architecture = architecture,
                Variant = variant,
                CustomBuildLegGroups = customBuildLegGroups ?? Array.Empty<CustomBuildLegGroup>()
            };
        }

        public static Platform CreatePlatformWithRepoBuildArg(string dockerfilePath, string repo, string[] tags, OS os = OS.Linux)
        {
            Platform platform = ManifestHelper.CreatePlatform(dockerfilePath, tags, os);
            platform.BuildArgs = new Dictionary<string, string>
            {
                { "REPO", repo }
            };
            return platform;
        }

        public static void AddVariable(Manifest manifest, string name, string value)
        {
            if (manifest.Variables == null)
            {
                manifest.Variables = new Dictionary<string, string>();
            }

            manifest.Variables.Add(name, value);
        }

        public static IManifestOptionsInfo GetManifestOptions(string manifestPath)
        {
            Mock<IManifestOptionsInfo> manifestOptionsMock = new Mock<IManifestOptionsInfo>();

            manifestOptionsMock
                .SetupGet(o => o.Manifest)
                .Returns(manifestPath);

            manifestOptionsMock
                .Setup(o => o.GetManifestFilter())
                .Returns(new ManifestFilter(Enumerable.Empty<string>()));

            return manifestOptionsMock.Object;
        }
    }
}
