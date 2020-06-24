// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PlatformInfoTests
    {
        [Theory]
        [InlineData("debian", "Debian")]
        [InlineData("jessie", "Debian 8")]
        [InlineData("stretch", "Debian 9")]
        [InlineData("stretch-slim", "Debian 9")]
        [InlineData("buster", "Debian 10")]
        [InlineData("buster-slim", "Debian 10")]
        [InlineData("xenial", "Ubuntu 16.04")]
        [InlineData("bionic", "Ubuntu 18.04")]
        [InlineData("disco", "Ubuntu 19.04")]
        [InlineData("focal", "Ubuntu 20.04")]
        [InlineData("alpine3.12", "Alpine 3.12")]
        [InlineData("centos8", "Centos 8")]
        [InlineData("fedora32", "Fedora 32")]
        public void GetOSDisplayName_Linux(string osVersion, string expectedDisplayName)
        {
            ValidateGetOSDisplayName(OS.Linux, osVersion, expectedDisplayName);
        }

        [Theory]
        [InlineData("windowsservercore-ltsc2016", "Windows Server 2016")]
        [InlineData("windowsservercore-ltsc2019", "Windows Server 2019")]
        [InlineData("nanoserver-1809", "Windows Server 2019")]
        [InlineData("windowsservercore-1903", "Windows Server, version 1903")]
        [InlineData("nanoserver-1903", "Windows Server, version 1903")]
        public void GetOSDisplayName_Windows(string osVersion, string expectedDisplayName)
        {
            ValidateGetOSDisplayName(OS.Windows, osVersion, expectedDisplayName);
        }

        private void ValidateGetOSDisplayName(OS os, string osVersion, string expectedDisplayName)
        {
            Platform platform = CreatePlatform("runtime/2.1", new string[] { "runtime" }, os: os, osVersion: osVersion);
            VariableHelper variableHelper = new VariableHelper(new Manifest(), Mock.Of<IManifestOptionsInfo>(), null);
            PlatformInfo platformInfo = PlatformInfo.Create(platform, "", "runtime", variableHelper, "./");

            Assert.Equal(expectedDisplayName, platformInfo.GetOSDisplayName());
        }
    }
}
