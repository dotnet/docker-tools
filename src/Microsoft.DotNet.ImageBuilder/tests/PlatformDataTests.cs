// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PlatformDataTests
    {
        [Theory]
        [InlineData("5.0.0-preview.3", "5.0")]
        [InlineData("5.0", "5.0")]
        [InlineData("5.0.1", "5.0")]
        public void GetIdentifier(string productVersion, string expectedVersion)
        {
            PlatformData platform = new PlatformData
            {
                ImageInfo = CreateImage(productVersion),
                Architecture = "amd64",
                OsType = "linux",
                OsVersion = "focal",
                Dockerfile = "path"
            };

            string identifier = platform.GetIdentifier();
            Assert.Equal($"path-amd64-linux-focal-{expectedVersion}", identifier);
        }

        private static ImageInfo CreateImage(string productVersion) =>
            ImageInfo.Create(
                new Image
                {
                    Platforms = Array.Empty<Platform>(),
                    ProductVersion = productVersion
                },
                "fullrepo",
                "repo",
                new ManifestFilter(Enumerable.Empty<string>()),
                new VariableHelper(new Manifest(), Mock.Of<IManifestOptionsInfo>(), null),
                "base");
    }
}
