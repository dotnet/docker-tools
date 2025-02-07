// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.DockerTools.ImageBuilder.ViewModel;
using Xunit;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Tests
{
    public class ModelExtensionsTests
    {
        [Theory]
        [InlineData(Architecture.AMD64, "amd64")]
        [InlineData(Architecture.ARM, "arm32")]
        [InlineData(Architecture.ARM64, "arm64")]
        [InlineData(Architecture.AMD64, "amd64v8", "v8")]
        [InlineData(Architecture.ARM, "arm32v7", "V7")]
        public void Architecture_GetDisplayName(Architecture architecture, string expectedDisplayName, string variant = null)
        {
            Assert.Equal(expectedDisplayName, architecture.GetDisplayName(variant));
        }

        [Theory]
        [InlineData(Architecture.AMD64, "x64")]
        [InlineData(Architecture.ARM, "arm")]
        [InlineData(Architecture.ARM64, "arm64")]
        public void Architecture_GetShortName(Architecture architecture, string expectedShortName)
        {
            Assert.Equal(expectedShortName, architecture.GetShortName());
        }

        [Theory]
        [InlineData(Architecture.AMD64, "x64")]
        [InlineData(Architecture.ARM, "arm32")]
        [InlineData(Architecture.ARM64, "arm64")]
        public void Architecture_GetNupkgName(Architecture architecture, string expectedNupkgName)
        {
            Assert.Equal(expectedNupkgName, architecture.GetNupkgName());
        }
    }
}
