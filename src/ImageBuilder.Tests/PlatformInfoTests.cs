// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
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
        [InlineData("trixie", "Debian 13")]
        [InlineData("trixie-slim", "Debian 13")]
        [InlineData("noble", "Ubuntu 24.04")]
        [InlineData("noble-chiseled", "Ubuntu 24.04")]
        [InlineData("alpine3.12", "Alpine 3.12")]
        [InlineData("centos8", "Centos 8")]
        [InlineData("fedora32", "Fedora 32")]
        [InlineData("cbl-mariner2.0", "CBL-Mariner 2.0")]
        [InlineData("azurelinux3.0", "Azure Linux 3.0")]
        [InlineData("azurelinux3.0-distroless", "Azure Linux 3.0")]
        public void GetOSDisplayName_Linux(string osVersion, string expectedDisplayName)
        {
            ValidateGetOSDisplayName(OS.Linux, osVersion, expectedDisplayName);
        }

        [Theory]
        [InlineData("windowsservercore-ltsc2016", "Windows Server Core 2016")]
        [InlineData("windowsservercore-ltsc2019", "Windows Server Core 2019")]
        [InlineData("nanoserver-1809", "Nano Server, version 1809")]
        [InlineData("windowsservercore-1903", "Windows Server Core, version 1903")]
        [InlineData("nanoserver-1903", "Nano Server, version 1903")]
        [InlineData("nanoserver-ltsc2022", "Nano Server 2022")]
        public void GetOSDisplayName_Windows(string osVersion, string expectedDisplayName)
        {
            ValidateGetOSDisplayName(OS.Windows, osVersion, expectedDisplayName);
        }

        [Theory]
        [InlineData("VALID", "ubuntu:latest", "$VALID", "ubuntu:latest")]
        [InlineData("VALID_123", "alpine:latest", "$VALID_123", "alpine:latest")]
        [InlineData("VALID_123", "alpine:latest", "$VALID_123-other", "alpine:latest-other")]
        public void Initialize_ArgPattern(
            string buildArgKey, string buildArgValue, string fromTag, string expectedFromImage)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            
            // Create a dockerfile with the ARG
            string dockerfileContent = $"ARG {buildArgKey}\nFROM {fromTag}";
            string dockerfilePath = Path.Combine(tempFolderContext.Path, "Dockerfile");
            File.WriteAllText(dockerfilePath, dockerfileContent);
            
            Platform platform = CreatePlatform("Dockerfile", [ "test" ]);
            platform.BuildArgs = new Dictionary<string, string>
            {
                { buildArgKey, buildArgValue }
            };
            
            VariableHelper variableHelper = new(new Manifest(), Mock.Of<IManifestOptionsInfo>(), null);
            PlatformInfo platformInfo = PlatformInfo.Create(
                platform, "", "test", variableHelper, tempFolderContext.Path);
            platformInfo.Initialize([], "test.azurecr.io");
            
            Assert.Contains(expectedFromImage, platformInfo.ExternalFromImages);
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
