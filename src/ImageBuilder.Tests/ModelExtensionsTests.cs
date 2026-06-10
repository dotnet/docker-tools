#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class ModelExtensionsTests
    {
        [TestMethod]
        [DataRow(Architecture.AMD64, "amd64")]
        [DataRow(Architecture.ARM, "arm32")]
        [DataRow(Architecture.ARM64, "arm64")]
        [DataRow(Architecture.AMD64, "amd64v8", "v8")]
        [DataRow(Architecture.ARM, "arm32v7", "V7")]
        public void Architecture_GetDisplayName(Architecture architecture, string expectedDisplayName, string variant = null)
        {
            architecture.GetDisplayName(variant).ShouldBe(expectedDisplayName);
        }

        [TestMethod]
        [DataRow(Architecture.AMD64, "x64")]
        [DataRow(Architecture.ARM, "arm")]
        [DataRow(Architecture.ARM64, "arm64")]
        public void Architecture_GetShortName(Architecture architecture, string expectedShortName)
        {
            architecture.GetShortName().ShouldBe(expectedShortName);
        }

        [TestMethod]
        [DataRow(Architecture.AMD64, "x64")]
        [DataRow(Architecture.ARM, "arm32")]
        [DataRow(Architecture.ARM64, "arm64")]
        public void Architecture_GetNupkgName(Architecture architecture, string expectedNupkgName)
        {
            architecture.GetNupkgName().ShouldBe(expectedNupkgName);
        }
    }
}
