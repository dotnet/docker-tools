#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class PlatformInfoTests
    {
        [TestMethod]
        [DataRow("debian", "Debian")]
        [DataRow("trixie", "Debian 13")]
        [DataRow("trixie-slim", "Debian 13")]
        [DataRow("noble", "Ubuntu 24.04")]
        [DataRow("noble-chiseled", "Ubuntu 24.04")]
        [DataRow("resolute", "Ubuntu 26.04")]
        [DataRow("resolute-chiseled", "Ubuntu 26.04")]
        [DataRow("alpine3.12", "Alpine 3.12")]
        [DataRow("centos8", "Centos 8")]
        [DataRow("fedora32", "Fedora 32")]
        [DataRow("cbl-mariner2.0", "CBL-Mariner 2.0")]
        [DataRow("azurelinux3.0", "Azure Linux 3.0")]
        [DataRow("azurelinux3.0-distroless", "Azure Linux 3.0")]
        public void GetOSDisplayName_Linux(string osVersion, string expectedDisplayName)
        {
            ValidateGetOSDisplayName(OS.Linux, osVersion, expectedDisplayName);
        }

        [TestMethod]
        [DataRow("windowsservercore-ltsc2016", "Windows Server Core 2016")]
        [DataRow("windowsservercore-ltsc2019", "Windows Server Core 2019")]
        [DataRow("nanoserver-1809", "Nano Server, version 1809")]
        [DataRow("windowsservercore-1903", "Windows Server Core, version 1903")]
        [DataRow("nanoserver-1903", "Nano Server, version 1903")]
        [DataRow("nanoserver-ltsc2022", "Nano Server 2022")]
        public void GetOSDisplayName_Windows(string osVersion, string expectedDisplayName)
        {
            ValidateGetOSDisplayName(OS.Windows, osVersion, expectedDisplayName);
        }

        [TestMethod]
        [DataRow("VALID", "ubuntu:latest", "$VALID", "ubuntu:latest")]
        [DataRow("VALID_123", "alpine:latest", "$VALID_123", "alpine:latest")]
        [DataRow("VALID_123", "alpine:latest", "$VALID_123-other", "alpine:latest-other")]
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
            
            platformInfo.ExternalFromImages.ShouldContain(expectedFromImage);
        }

        private void ValidateGetOSDisplayName(OS os, string osVersion, string expectedDisplayName)
        {
            Platform platform = CreatePlatform("runtime/2.1", new string[] { "runtime" }, os: os, osVersion: osVersion);
            VariableHelper variableHelper = new VariableHelper(new Manifest(), Mock.Of<IManifestOptionsInfo>(), null);
            PlatformInfo platformInfo = PlatformInfo.Create(platform, "", "runtime", variableHelper, "./");

            platformInfo.GetOSDisplayName().ShouldBe(expectedDisplayName);
        }
    }
}
