// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

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
            return new Repo
            {
                Name = name,
                Images = images
            };
        }

        public static Image CreateImage(params Platform[] platforms)
        {
            return new Image
            {
                Platforms = platforms
            };
        }

        public static Platform CreatePlatform(string dockerfilePath, string[] tags, OS os = OS.Linux)
        {
            return new Platform
            {
                Dockerfile = dockerfilePath,
                OsVersion = "version",
                OS = os,
                Tags = tags.ToDictionary(tag => tag, tag => new Tag()),
                Architecture = Architecture.AMD64
            };
        }
    }
}
