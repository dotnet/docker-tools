#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class GenerateBuildMatrixCommandTests
    {
        /// <summary>
        /// Verifies the platformVersionedOs matrix type.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/243
        /// </remarks>
        [TestMethod]
        [DataRow(null, "--path 2.1.1/runtime-deps/os --path 2.2/runtime/os", "2.1.1")]
        [DataRow("--path 2.2/runtime/os", "--path 2.2/runtime/os", "2.2")]
        [DataRow("--path 2.1.1/runtime-deps/os", "--path 2.1.1/runtime-deps/os", "2.1.1")]
        public async Task GenerateBuildMatrixCommand_PlatformVersionedOs(string filterPaths, string expectedPaths, string verificationLegName)
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                GenerateBuildMatrixCommand command = CreateCommand();
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.MatrixType = MatrixType.PlatformVersionedOs;
                command.Options.ProductVersionComponents = 3;
                if (filterPaths != null)
                {
                    command.Options.FilterOptions.Dockerfile.Paths = filterPaths.Replace("--path ", "").Split(" ");
                }

                const string runtimeDepsRelativeDir = "2.1.1/runtime-deps/os";
                DirectoryInfo runtimeDepsDir = Directory.CreateDirectory(
                    Path.Combine(tempFolderContext.Path, runtimeDepsRelativeDir));
                string dockerfileRuntimeDepsFullPath = Path.Combine(runtimeDepsDir.FullName, "Dockerfile");
                File.WriteAllText(dockerfileRuntimeDepsFullPath, "FROM base");

                const string runtimeRelativeDir = "2.2/runtime/os";
                DirectoryInfo runtimeDir = Directory.CreateDirectory(
                    Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRuntimePath = Path.Combine(runtimeDir.FullName, "Dockerfile");
                File.WriteAllText(dockerfileRuntimePath, "FROM runtime-deps:tag");

                Manifest manifest = CreateManifest(
                    CreateRepo("runtime-deps",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(runtimeDepsRelativeDir, new string[] { "tag" })
                            },
                            productVersion: "2.1.1")),
                    CreateRepo("runtime",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(runtimeRelativeDir, new string[] { "runtime" })
                            },
                            productVersion: "2.2.3-preview"))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
                matrixInfos.ShouldHaveSingleItem();

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                BuildLegInfo leg = matrixInfo.Legs.First(leg => leg.Name.StartsWith(verificationLegName));
                string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                imageBuilderPaths.ShouldBe(expectedPaths);
            }
        }

        /// <summary>
        /// Verifies the PlatformDependencyGraph matrix type.
        /// </summary>
        /// <remarks>
        /// Test uses a dependency graph of runtimedeps <- sdk <- sample
        /// </remarks>
        [TestMethod]
        [DataRow(null, "--path 1.0/runtimedeps/os/amd64 --path 1.0/sdk/os/amd64 --path 1.0/sample/os/amd64")]
        [DataRow("--path 1.0/runtimedeps/os/amd64", "--path 1.0/runtimedeps/os/amd64")]
        [DataRow("--path 1.0/runtimedeps/os/amd64 --path 1.0/sdk/os/amd64", "--path 1.0/runtimedeps/os/amd64 --path 1.0/sdk/os/amd64")]
        [DataRow("--path 1.0/sdk/os/amd64", "--path 1.0/sdk/os/amd64")]
        [DataRow("--path 1.0/sdk/os/amd64 --path 1.0/sample/os/amd64", "--path 1.0/sdk/os/amd64 --path 1.0/sample/os/amd64")]
        public async Task GenerateBuildMatrixCommand_PlatformDependencyGraph(string filterPaths, string expectedPaths)
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                GenerateBuildMatrixCommand command = CreateCommand();
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
                if (filterPaths != null)
                {
                    command.Options.FilterOptions.Dockerfile.Paths = filterPaths.Replace("--path ", "").Split(" ");
                }

                const string runtimeDepsRelativeDir = "1.0/runtimedeps/os/amd64";
                DirectoryInfo runtimeDepsDir = Directory.CreateDirectory(
                    Path.Combine(tempFolderContext.Path, runtimeDepsRelativeDir));
                string dockerfileRuntimeDepsFullPath = Path.Combine(runtimeDepsDir.FullName, "Dockerfile");
                File.WriteAllText(dockerfileRuntimeDepsFullPath, "FROM base");

                const string sdkRelativeDir = "1.0/sdk/os/amd64";
                DirectoryInfo sdkDir = Directory.CreateDirectory(
                    Path.Combine(tempFolderContext.Path, sdkRelativeDir));
                string dockerfileSdkPath = Path.Combine(sdkDir.FullName, "Dockerfile");
                File.WriteAllText(dockerfileSdkPath, "FROM runtime-deps:tag");

                const string sampleRelativeDir = "1.0/sample/os/amd64";
                DirectoryInfo sampleDir = Directory.CreateDirectory(
                    Path.Combine(tempFolderContext.Path, sampleRelativeDir));
                string dockerfileSamplePath = Path.Combine(sampleDir.FullName, "Dockerfile");
                File.WriteAllText(dockerfileSamplePath, "FROM sdk:tag");

                Manifest manifest = CreateManifest(
                    CreateRepo("runtime-deps",
                        CreateImage(
                            CreatePlatform(runtimeDepsRelativeDir, new string[] { "tag" }))),
                    CreateRepo("sdk",
                        CreateImage(
                            CreatePlatform(sdkRelativeDir, new string[] { "tag" }))),
                    CreateRepo("sample",
                        CreateImage(
                            CreatePlatform(sampleRelativeDir, new string[] { "tag" })))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
                matrixInfos.ShouldHaveSingleItem();

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                BuildLegInfo leg = matrixInfo.Legs.First();
                string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                imageBuilderPaths.ShouldBe(expectedPaths);
            }
        }

        private static void SetCacheResult(Mock<IImageCacheService> imageCacheServiceMock, string dockerfilePath, ImageCacheState cacheState)
        {
            imageCacheServiceMock
                .Setup(o => o.CheckForCachedImageAsync(
                    It.IsAny<ImageData>(),
                    It.Is<PlatformData>(platform => platform.Dockerfile == dockerfilePath),
                    It.IsAny<ImageDigestCache>(),
                    It.IsAny<ImageNameResolver>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(new ImageCacheResult(cacheState, false, null));
        }

        [TestMethod]
        [DataRow(
            ImageCacheState.NotCached,
            ImageCacheState.NotCached,
            "--path 1.0/runtime/os/amd64/Dockerfile --path 1.0/sdk/os/amd64/Dockerfile",
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [DataRow(
            ImageCacheState.Cached,
            ImageCacheState.Cached,
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [DataRow(
            ImageCacheState.Cached,
            ImageCacheState.Cached,
            "--path 1.0/standalone/os/amd64/Dockerfile",
            "--path 2.0/standalone/os/amd64/Dockerfile",
            null,
            "*standalone*")]
        [DataRow(
            ImageCacheState.CachedWithMissingTags,
            ImageCacheState.Cached,
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [DataRow(
            ImageCacheState.Cached,
            ImageCacheState.NotCached,
            "--path 1.0/runtime/os/amd64/Dockerfile --path 1.0/sdk/os/amd64/Dockerfile",
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [DataRow(
            ImageCacheState.NotCached,
            ImageCacheState.NotCached,
            "--path 1.0/runtime/os/amd64/Dockerfile --path 1.0/sdk/os/amd64/Dockerfile",
            "--path 1.0/standalone/os/amd64/Dockerfile",
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile",
            "")] // Clear out the path filters to ensure all images are included
        public async Task FilterOutCachedImages(
            ImageCacheState runtime1CacheState,
            ImageCacheState sdk1CacheState,
            string leg1ExpectedPaths,
            string leg2ExpectedPaths = null,
            string leg3ExpectedPaths = null,
            string inputPathFilters = "*runtime* *sdk*")
        {
            const string Standalone1RelativeDir = "1.0/standalone/os/amd64";
            string dockerfileStandalone1Path;
            const string Standalone2RelativeDir = "2.0/standalone/os/amd64";
            string dockerfileStandalone2Path;

            const string Runtime1RelativeDir = "1.0/runtime/os/amd64";
            string dockerfileRuntime1Path;
            const string Runtime2RelativeDir = "2.0/runtime/os/amd64";
            string dockerfileRuntime2Path;

            const string Sdk1RelativeDir = "1.0/sdk/os/amd64";
            string dockerfileSdk1Path;
            const string Sdk2RelativeDir = "2.0/sdk/os/amd64";
            string dockerfileSdk2Path;

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Manifest manifest = CreateManifest(
                CreateRepo("standalone",
                    CreateImage(
                        CreatePlatform(dockerfileStandalone1Path = CreateDockerfile(Standalone1RelativeDir, tempFolderContext), ["1.0"])),
                    CreateImage(
                        // This image will not be included in the image info file. It is intended to be a newly added Dockerfile that hasn't
                        // been published yet.
                        CreatePlatform(dockerfileStandalone2Path = CreateDockerfile(Standalone2RelativeDir, tempFolderContext), ["2.0"]))),
                CreateRepo("runtime",
                    CreateImage(
                        CreatePlatform(dockerfileRuntime1Path = CreateDockerfile(Runtime1RelativeDir, tempFolderContext, "base"), ["1.0"])),
                    CreateImage(
                        CreatePlatform(dockerfileRuntime2Path = CreateDockerfile(Runtime2RelativeDir, tempFolderContext, "base"), ["2.0"]))),
                CreateRepo("sdk",
                    CreateImage(
                        CreatePlatform(dockerfileSdk1Path = CreateDockerfile(Sdk1RelativeDir, tempFolderContext, "runtime:1.0"), ["1.0"])),
                    CreateImage(
                        CreatePlatform(dockerfileSdk2Path = CreateDockerfile(Sdk2RelativeDir, tempFolderContext, "runtime:2.0"), ["2.0"])))
            );

            Mock<IImageCacheService> imageCacheServiceMock = new();
            SetCacheResult(imageCacheServiceMock, dockerfileStandalone1Path, ImageCacheState.NotCached);
            SetCacheResult(imageCacheServiceMock, dockerfileRuntime1Path, runtime1CacheState);
            SetCacheResult(imageCacheServiceMock, dockerfileSdk1Path, sdk1CacheState);
            SetCacheResult(imageCacheServiceMock, dockerfileRuntime2Path, ImageCacheState.NotCached);
            SetCacheResult(imageCacheServiceMock, dockerfileSdk2Path, ImageCacheState.NotCached);

            GenerateBuildMatrixCommand command = new(
                TestHelper.CreateManifestJsonService(),
                CreateImageInfoService(),
                imageCacheServiceMock.Object,
                Mock.Of<IManifestServiceFactory>(),
                Mock.Of<ILogger<GenerateBuildMatrixCommand>>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "imageinfo.json");
            command.Options.TrimCachedImages = true;
            command.Options.FilterOptions.Dockerfile.Paths = inputPathFilters.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "standalone",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreateSimplePlatformData(dockerfileStandalone1Path)
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    // Include a Dockerfile which doesn't exist in the manifest to simulate a deleted Dockerfile
                                    CreateSimplePlatformData("0.0/standalone/os/amd64/Dockerfile"),
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreateSimplePlatformData(dockerfileRuntime1Path)
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreateSimplePlatformData(dockerfileRuntime2Path)
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "sdk",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreateSimplePlatformData(dockerfileSdk1Path)
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreateSimplePlatformData(dockerfileSdk2Path)
                                }
                            }
                        }
                    }
                }
            };
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            BuildLegInfo leg = matrixInfo.Legs.First();
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe(leg1ExpectedPaths);

            if (leg2ExpectedPaths is not null)
            {
                leg = matrixInfo.Legs.Skip(1).First();
                imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                imageBuilderPaths.ShouldBe(leg2ExpectedPaths);
            }

            if (leg3ExpectedPaths is not null)
            {
                leg = matrixInfo.Legs.Skip(2).First();
                imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                imageBuilderPaths.ShouldBe(leg3ExpectedPaths);
            }
        }

        /// <summary>
        /// Verifies that the correct matrix is generated when the DistinctMatrixOsVersion option is set.
        /// </summary>
        [TestMethod]
        public async Task GenerateBuildMatrixCommand_CustomMatrixOsVersion()
        {
            const string CustomMatrixOsVersion = "custom";
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
            command.Options.DistinctMatrixOsVersions = new string[]
            {
                CustomMatrixOsVersion
            };

            Manifest manifest = CreateManifest(
                CreateRepo("aspnet",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/aspnet/os/amd64", tempFolderContext),
                            new string[] { "os" })),
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/aspnet/os2/amd64", tempFolderContext),
                            new string[] { "os2" },
                            osVersion: CustomMatrixOsVersion))),
                CreateRepo("sdk",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/sdk/os/amd64", tempFolderContext, "aspnet:os"),
                            new string[] { "tag" })),
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/sdk/os2/amd64", tempFolderContext, "aspnet:os2"),
                            new string[] { "tag2" },
                            osVersion: CustomMatrixOsVersion)))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.Count().ShouldBe(2);

            BuildMatrixInfo matrixInfo = matrixInfos.ElementAt(0);
            matrixInfo.Name.ShouldBe($"{CustomMatrixOsVersion}Amd64");
            matrixInfo.Legs.ShouldHaveSingleItem();
            (matrixInfo.Legs.First().Variables.First(variable => variable.Name == "imageBuilderPaths").Value).ShouldBe("--path 1.0/aspnet/os2/amd64/Dockerfile --path 1.0/sdk/os2/amd64/Dockerfile");

            matrixInfo = matrixInfos.ElementAt(1);
            matrixInfo.Name.ShouldBe($"linuxAmd64");
            matrixInfo.Legs.ShouldHaveSingleItem();
            (matrixInfo.Legs.First().Variables.First(variable => variable.Name == "imageBuilderPaths").Value).ShouldBe("--path 1.0/aspnet/os/amd64/Dockerfile --path 1.0/sdk/os/amd64/Dockerfile");
        }

        /// <summary>
        /// Verifies that <see cref="Platform.BuildOsVersion"/> overrides the build host/agent
        /// (matrix bucket) selection without changing the image's base OS version. Two Windows
        /// images with different base OS versions (Server 2019 and Server 2025) should be grouped
        /// into a single Server 2025 build matrix when the 2019 image sets its build OS to 2025,
        /// while each leg still builds against its own base OS version.
        /// </summary>
        [TestMethod]
        public async Task GenerateBuildMatrixCommand_BuildOsVersionOverride()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/windowsservercore-ltsc2019", tempFolderContext),
                            new string[] { "ltsc2019" },
                            os: OS.Windows,
                            osVersion: "windowsservercore-ltsc2019",
                            buildOsVersion: "windowsservercore-ltsc2025")),
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/windowsservercore-ltsc2025", tempFolderContext),
                            new string[] { "ltsc2025" },
                            os: OS.Windows,
                            osVersion: "windowsservercore-ltsc2025")))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();

            BuildMatrixInfo matrixInfo = matrixInfos.ShouldHaveSingleItem();
            matrixInfo.Name.ShouldBe("windowsLtsc2025Amd64");
            matrixInfo.Legs.Count.ShouldBe(2);

            foreach (BuildLegInfo leg in matrixInfo.Legs)
            {
                leg.Variables.First(variable => variable.Name == "osType").Value.ShouldBe("windows");
            }

            IEnumerable<string> osVersions = matrixInfo.Legs
                .Select(leg => leg.Variables.First(variable => variable.Name == "osVersions").Value);
            osVersions.ShouldContain("--os-version windowsservercore-ltsc2019");
            osVersions.ShouldContain("--os-version windowsservercore-ltsc2025");
        }

        /// <summary>
        /// Verifies the platformVersionedOs matrix type is generated correctly when a
        /// custom build leg group is defined that has a parent graph.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/243
        /// </remarks>
        [TestMethod]
        [DataRow(CustomBuildLegDependencyType.Integral)]
        [DataRow(CustomBuildLegDependencyType.Supplemental)]
        public async Task GenerateBuildMatrixCommand_CustomBuildLegGroupingParentGraph(CustomBuildLegDependencyType dependencyType)
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                const string customBuildLegGroup = "custom";
                GenerateBuildMatrixCommand command = CreateCommand();
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.MatrixType = MatrixType.PlatformVersionedOs;
                command.Options.ProductVersionComponents = 2;
                command.Options.CustomBuildLegGroups = new string[] { customBuildLegGroup };

                string dockerfileRuntimeDepsFullPath = DockerfileHelper.CreateDockerfile("1.0/runtime-deps/os", tempFolderContext);
                string dockerfileRuntimePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext, "runtime-deps:tag");

                string dockerfileRuntime2FullPath = DockerfileHelper.CreateDockerfile("2.0/runtime/os2", tempFolderContext);
                string dockerfileSdk2FullPath = DockerfileHelper.CreateDockerfile("2.0/sdk/os2", tempFolderContext, "runtime2:tag");

                Manifest manifest = CreateManifest(
                    CreateRepo("runtime-deps",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfileRuntimeDepsFullPath, new string[] { "tag" })
                            },
                            productVersion: "1.0")),
                    CreateRepo("runtime",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfileRuntimePath, new string[] { "runtime" },
                                    customBuildLegGroups: new CustomBuildLegGroup[]
                                    {
                                        new CustomBuildLegGroup
                                        {
                                            Name = customBuildLegGroup,
                                            Type = dependencyType,
                                            Dependencies = new string[]
                                            {
                                                "sdk2:tag"
                                            }
                                        }
                                    })
                            },
                            productVersion: "1.0")),
                    CreateRepo("runtime2",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfileRuntime2FullPath, new string[] { "tag" }, osVersion: "trixie-slim")
                            },
                            productVersion: "2.0")),
                    CreateRepo("sdk2",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfileSdk2FullPath, new string[] { "tag" }, osVersion: "trixie")
                            },
                            productVersion: "2.0"))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
                matrixInfos.ShouldHaveSingleItem();

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                matrixInfo.Legs.ShouldHaveSingleItem();
                BuildLegInfo leg_1_0 = matrixInfo.Legs.First();
                string imageBuilderPaths = leg_1_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                imageBuilderPaths.ShouldBe("--path 1.0/runtime-deps/os/Dockerfile --path 1.0/runtime/os/Dockerfile --path 2.0/sdk/os2/Dockerfile --path 2.0/runtime/os2/Dockerfile");
                string osVersions = leg_1_0.Variables.First(variable => variable.Name == "osVersions").Value;
                osVersions.ShouldBe("--os-version noble --os-version trixie --os-version trixie-slim");
            }
        }

        /// <summary>
        /// Verifies that a <see cref="MatrixType.PlatformDependencyGraph"/> build matrix can be generated correctly
        /// when there are both Windows Server Core and Nano Server platforms that have a custom build leg group
        /// dependency. The scenario for this is that, for a given matching version between Server Core and Nano Server,
        /// those Dockerfiles should be built in the same job.
        /// </summary>
        [TestMethod]
        public async Task GenerateBuildMatrixCommand_ServerCoreAndNanoServerDependency()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            const string customBuildLegGroup = "custom";
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
            command.Options.ProductVersionComponents = 2;
            command.Options.CustomBuildLegGroups = new string[] { customBuildLegGroup };

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/runtime/nanoserver-1909", tempFolderContext),
                                new string[] { "nanoserver-1909" },
                                os: OS.Windows,
                                osVersion: "nanoserver-1909")
                        },
                        productVersion: "1.0"),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/runtime/windowsservercore-1909", tempFolderContext),
                                new string[] { "windowsservercore-1909" },
                                os: OS.Windows,
                                osVersion: "windowsservercore-1909")
                        },
                        productVersion: "1.0")),
                CreateRepo("aspnet",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/aspnet/nanoserver-1909", tempFolderContext, "runtime:nanoserver-1909"),
                                new string[] { "nanoserver-1909" },
                                os: OS.Windows,
                                osVersion: "nanoserver-1909")
                        },
                        productVersion: "1.0"),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/aspnet/windowsservercore-1909", tempFolderContext, "runtime:windowsservercore-1909"),
                                new string[] { "windowsservercore-1909" },
                                os: OS.Windows,
                                osVersion: "windowsservercore-1909",
                                customBuildLegGroups: new CustomBuildLegGroup[]
                                {
                                    new CustomBuildLegGroup
                                    {
                                        Name = customBuildLegGroup,
                                        Type = CustomBuildLegDependencyType.Supplemental,
                                        Dependencies = new string[]
                                        {
                                            "aspnet:nanoserver-1909"
                                        }
                                    }
                                })
                        },
                        productVersion: "1.0"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            BuildLegInfo leg = matrixInfo.Legs.First();
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe("--path 1.0/runtime/nanoserver-1909/Dockerfile --path 1.0/aspnet/nanoserver-1909/Dockerfile --path 1.0/runtime/windowsservercore-1909/Dockerfile --path 1.0/aspnet/windowsservercore-1909/Dockerfile");
            string osVersions = leg.Variables.First(variable => variable.Name == "osVersions").Value;
            osVersions.ShouldBe("--os-version nanoserver-1909 --os-version windowsservercore-1909");
        }

        /// <summary>
        /// Verifies the platformVersionedOs matrix type is generated correctly when there's a dependency on a
        /// tag that's outside the platform group.
        /// </summary>
        [TestMethod]
        public async Task GenerateBuildMatrixCommand_ParentGraphOutsidePlatformGroup()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.ProductVersionComponents = 2;

            string dockerfileRuntimeFullPath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string dockerfileRuntime2FullPath = DockerfileHelper.CreateDockerfile("1.0/runtime2/os", tempFolderContext, "sdk3:tag");

            string dockerfileRuntime3FullPath = DockerfileHelper.CreateDockerfile("1.0/runtime3/os2", tempFolderContext);
            string dockerfileSdk3FullPath = DockerfileHelper.CreateDockerfile("1.0/sdk3/os2", tempFolderContext, "runtime3:tag");

            Manifest manifest = CreateManifest(
                // Define a Dockerfile that has the same OS version and product version as runtime2 but no actual dependency to
                // ensure it gets its own matrix leg.
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRuntimeFullPath, new string[] { "tag" }, osVersion: "trixie")
                        },
                        productVersion: "1.0")),
                CreateRepo("runtime2",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRuntime2FullPath, new string[] { "runtime" }, osVersion: "bullseye")
                        },
                        productVersion: "1.0")),
                CreateRepo("runtime3",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRuntime3FullPath, new string[] { "tag" }, osVersion: "alpine3.12")
                        },
                        productVersion: "1.0")),
                CreateRepo("sdk3",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileSdk3FullPath, new string[] { "tag" }, osVersion: "alpine3.12")
                        },
                        productVersion: "1.0"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            matrixInfo.Legs.Count.ShouldBe(2);
            BuildLegInfo leg_1_0 = matrixInfo.Legs.First();
            string imageBuilderPaths = leg_1_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe("--path 1.0/runtime/os/Dockerfile");

            BuildLegInfo leg_2_0 = matrixInfo.Legs.ElementAt(1);
            imageBuilderPaths = leg_2_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe("--path 1.0/runtime2/os/Dockerfile --path 1.0/sdk3/os2/Dockerfile --path 1.0/runtime3/os2/Dockerfile");
        }

        /// <summary>
        /// Verifies that a <see cref="MatrixType.PlatformVersionedOs"/> build matrix can be generated correctly
        /// when there are multiple custom build leg groups defined.
        /// </summary>
        [TestMethod]
        public async Task GenerateBuildMatrixCommand_MultiBuildLegGroups()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            const string customBuildLegGroup1 = "custom1";
            const string customBuildLegGroup2 = "custom2";
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.ProductVersionComponents = 2;
            command.Options.CustomBuildLegGroups = new string[] { customBuildLegGroup1, customBuildLegGroup2 };

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/repo1/os", tempFolderContext),
                                new string[] { "tag" },
                                osVersion: "bionic")
                        },
                        productVersion: "1.0")),
                CreateRepo("repo2",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/repo2/os", tempFolderContext),
                                new string[] { "tag" },
                                osVersion: "noble",
                                customBuildLegGroups: new CustomBuildLegGroup[]
                                {
                                    new CustomBuildLegGroup
                                    {
                                        Name = customBuildLegGroup1,
                                        Type = CustomBuildLegDependencyType.Supplemental,
                                        Dependencies = new string[]
                                        {
                                            "repo1:tag"
                                        }
                                    }
                                })
                        },
                        productVersion: "1.0")),
                CreateRepo("repo3",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/repo3/os", tempFolderContext),
                                new string[] { "tag" },
                                osVersion: "trixie")
                        },
                        productVersion: "1.0")),
                CreateRepo("repo4",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("1.0/repo4/os", tempFolderContext),
                                new string[] { "tag" },
                                osVersion: "bullseye",
                                customBuildLegGroups: new CustomBuildLegGroup[]
                                {
                                    new CustomBuildLegGroup
                                    {
                                        Name = customBuildLegGroup1,
                                        Type = CustomBuildLegDependencyType.Integral,
                                        Dependencies = new string[]
                                        {
                                            "repo3:tag"
                                        }
                                    }
                                })
                        },
                        productVersion: "1.0"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            matrixInfo.Legs.Count().ShouldBe(2);
            BuildLegInfo leg = matrixInfo.Legs.ElementAt(0);
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe("--path 1.0/repo1/os/Dockerfile --path 1.0/repo2/os/Dockerfile");

            leg = matrixInfo.Legs.ElementAt(1);
            imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe("--path 1.0/repo3/os/Dockerfile --path 1.0/repo4/os/Dockerfile");
        }

        /// <summary>
        /// Verifies the matrix produced by the platformVersionedOs matrix type for a scenario
        /// where the platforms in the image info are a result of cached images.
        /// </summary>
        [TestMethod]
        [DataRow(false, false, false, "--path 1.0/runtime/os/Dockerfile --path 1.0/aspnet/os/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [DataRow(true, false, false, "--path 1.0/aspnet/os/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [DataRow(true, true, false, "--path 1.0/sdk/os/Dockerfile")]
        [DataRow(true, true, true, null)]
        public async Task PlatformVersionedOs_Cached(bool isRuntimeCached, bool isAspnetCached, bool isSdkCached, string expectedPaths)
        {
            const string registry = "example.azurecr.io";
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IImageInfoService> imageInfoServiceMock = new();
            GenerateBuildMatrixCommand command = CreateCommand(imageInfoServiceMock.Object);
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.ProductVersionComponents = 2;

            string runtimeDockerfilePath;
            string aspnetDockerfilePath;
            string sdkDockerfilePath;
            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                runtimeDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext),
                                new string[] { "tag" })
                        },
                        productVersion: "1.0")),
                CreateRepo("aspnet",
                    CreateImage(
                        new Platform[]
                        {
                              CreatePlatform(
                                  aspnetDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/aspnet/os", tempFolderContext, "runtime:tag"),
                                  new string[] { "tag" })
                        },
                        productVersion: "1.0")),
                CreateRepo("sdk",
                    CreateImage(
                        new Platform[]
                        {
                              CreatePlatform(
                                  sdkDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/sdk/os", tempFolderContext, "aspnet:tag"),
                                  new string[] { "tag" })
                        },
                        productVersion: "1.0"))
            );
            manifest.ImageInfo = new ImageInfoArtifact
            {
                Repo = "image-info",
                Tags = new Dictionary<string, Tag>
                {
                    { "latest", new Tag() }
                }
            };

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(runtimeDockerfilePath, isCached: isRuntimeCached)
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "aspnet",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(aspnetDockerfilePath, isCached: isAspnetCached)
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "sdk",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(sdkDockerfilePath, isCached: isSdkCached)
                                }
                            }
                        }
                    }
                }
            };
            command.Options.ImageInfoRegistryOverride = registry;
            command.Options.ImageInfoRepoPrefix = "prefix/";
            imageInfoServiceMock
                .Setup(service => service.PullImageInfoArtifactAsync(
                    It.IsAny<ManifestInfo>(),
                    registry,
                    command.Options.ImageInfoRepoPrefix,
                    default))
                .ReturnsAsync(JsonHelper.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();

            if (isRuntimeCached && isAspnetCached && isSdkCached)
            {
                matrixInfos.ShouldBeEmpty();
            }
            else
            {
                matrixInfos.ShouldHaveSingleItem();
                matrixInfos.First().Legs.ShouldHaveSingleItem();

                BuildLegInfo buildLeg = matrixInfos.First().Legs.First();
                string imageBuilderPaths = buildLeg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                imageBuilderPaths.ShouldBe(expectedPaths);
            }
        }

        [TestMethod]
        public async Task PlatformVersionedOs_ImageInfoArtifact_UsesManifestRegistryWhenNoOverrideIsSet()
        {
            const string registry = "example.azurecr.io";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IImageInfoService> imageInfoServiceMock = new();
            GenerateBuildMatrixCommand command = CreateCommand(imageInfoServiceMock.Object);
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.FilterOptions.Dockerfile.Paths = ["not-matching"];

            string dockerfile = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            Manifest manifest = CreateManifest(
                CreateRepo(
                    "runtime",
                    CreateImage(CreatePlatform(dockerfile, ["tag"]))));
            manifest.Registry = registry;
            manifest.ImageInfo = new ImageInfoArtifact
            {
                Repo = "image-info",
                Tags = new Dictionary<string, Tag>
                {
                    { "latest", new Tag() }
                }
            };

            File.WriteAllText(command.Options.Manifest, JsonConvert.SerializeObject(manifest));

            imageInfoServiceMock
                .Setup(service => service.PullImageInfoArtifactAsync(
                    It.IsAny<ManifestInfo>(),
                    registry,
                    null,
                    default))
                .ReturnsAsync(JsonHelper.SerializeObject(new ImageArtifactDetails()));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();

            matrixInfos.ShouldBeEmpty();
            imageInfoServiceMock.VerifyAll();
        }

        /// <summary>
        /// Verifies that the platformVersionedOs matrix type doesn't create mutiple legs
        /// for a scenario where platforms in the same repo share a cached parent image.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/1141
        /// </remarks>
        [TestMethod]
        [DataRow(false, false, "--path 1.0/runtime/os/Dockerfile --path 1.0/aspnet/os-composite/Dockerfile --path 1.0/aspnet/os/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [DataRow(true, false, "--path 1.0/aspnet/os/Dockerfile --path 1.0/aspnet/os-composite/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [DataRow(true, true, "--path 1.0/aspnet/os-composite/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        public async Task PlatformVersionedOs_CachedParent(bool isRuntimeCached, bool isAspnetCached, string expectedPaths)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "imageinfo.json");
            command.Options.ProductVersionComponents = 2;

            string runtimeDepsDockerfilePath;
            string runtimeDockerfilePath;
            string aspnetDockerfilePath;
            string aspnetCompositeDockerfilePath;
            string sdkDockerfilePath;

            Manifest manifest = CreateManifest(
                CreateRepo("runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                runtimeDepsDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime-deps/os", tempFolderContext),
                                new string[] { "tag" })
                        },
                        productVersion: "1.0")),
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                runtimeDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext, "runtime-deps:tag"),
                                new string[] { "tag" })
                        },
                        productVersion: "1.0")),
                CreateRepo("aspnet", new[]
                    {
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(
                                    aspnetDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/aspnet/os", tempFolderContext, "runtime:tag"),
                                    new string[] { "tag" })
                            },
                            productVersion: "1.0"),
                    CreateImage(
                        new Platform[]
                        {
                              CreatePlatform(
                                  aspnetCompositeDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/aspnet/os-composite", tempFolderContext, "runtime-deps:tag"),
                                  new string[] { "tag-composite" })
                        },
                        productVersion: "1.0")
                    }),
                CreateRepo("sdk",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                sdkDockerfilePath = DockerfileHelper.CreateDockerfile("1.0/sdk/os", tempFolderContext, "aspnet:tag"),
                                new string[] { "tag" })
                        },
                        productVersion: "1.0"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime-deps",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(runtimeDepsDockerfilePath, isCached: true)
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(runtimeDockerfilePath, isCached: isRuntimeCached)
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "aspnet",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(aspnetDockerfilePath, isCached: isAspnetCached)
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "aspnet",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(aspnetCompositeDockerfilePath, isCached: false)
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "sdk",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    CreateSimplePlatformData(sdkDockerfilePath, isCached: false)
                                }
                            }
                        }
                    },
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();

            matrixInfos.ShouldHaveSingleItem();
            matrixInfos.First().Legs.ShouldHaveSingleItem();

            BuildLegInfo buildLeg = matrixInfos.First().Legs.First();
            string imageBuilderPaths = buildLeg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe(expectedPaths);
        }

        [TestMethod]
        public async Task PlatformDependencyGraph_CrossReferencedDockerfileFromMultipleRepos_SingleDockerfile()
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
            command.Options.ProductVersionComponents = 2;

            const string runtimeDepsRelativeDir = "3.1/runtime-deps/os";
            DirectoryInfo runtimeDepsDir = Directory.CreateDirectory(
                Path.Combine(tempFolderContext.Path, runtimeDepsRelativeDir));
            string dockerfileRuntimeDepsFullPath = Path.Combine(runtimeDepsDir.FullName, "Dockerfile");
            File.WriteAllText(dockerfileRuntimeDepsFullPath, "FROM base");

            Manifest manifest = CreateManifest(
                CreateRepo("core/runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsRelativeDir, new string[] { "tag" })
                        },
                        productVersion: "3.1")),
                CreateRepo("runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsRelativeDir, new string[] { "tag" })
                        },
                        productVersion: "5.0")),
                CreateRepo("new/runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsRelativeDir, new string[] { "tag" })
                        },
                        productVersion: "5.1"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            matrixInfo.Legs.ShouldHaveSingleItem();

            matrixInfo.Legs[0].Name.ShouldBe("3.1-runtime-deps-os");
            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe($"--path {runtimeDepsRelativeDir}");
        }

        [TestMethod]
        [DataRow(MatrixType.PlatformVersionedOs)]
        [DataRow(MatrixType.PlatformDependencyGraph)]
        public async Task CrossReferencedDockerfileFromMultipleRepos_ImageGraph(MatrixType matrixType)
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = matrixType;
            command.Options.ProductVersionComponents = 2;

            Manifest manifest = CreateManifest(
                CreateRepo("core/runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("3.1/runtime-deps/os", tempFolderContext),
                                new string[] { "tag" })
                        },
                        productVersion: "3.1")),
                CreateRepo("core/runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("3.1/runtime/os", tempFolderContext, "core/runtime-deps:tag"),
                                new string[] { "tag" })
                        },
                        productVersion: "3.1")),
                CreateRepo("runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                "3.1/runtime-deps/os/Dockerfile",
                                new string[] { "tag" })
                        },
                        productVersion: "5.0")),
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("5.0/runtime/os", tempFolderContext, "runtime-deps:tag"),
                                new string[] { "tag" })
                        },
                        productVersion: "5.0"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();
            BuildMatrixInfo matrixInfo = matrixInfos.First();

            if (matrixType == MatrixType.PlatformDependencyGraph)
            {
                matrixInfo.Legs.ShouldHaveSingleItem();

                matrixInfo.Legs[0].Name.ShouldBe("3.1-runtime-deps-os-Dockerfile-graph");
                string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                imageBuilderPaths.ShouldBe($"--path 3.1/runtime-deps/os/Dockerfile --path 3.1/runtime/os/Dockerfile --path 5.0/runtime/os/Dockerfile");
            }
            else
            {
                matrixInfo.Legs.Count.ShouldBe(2);

                matrixInfo.Legs[0].Name.ShouldBe("3.1-noble-core-runtime-deps");
                string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                imageBuilderPaths.ShouldBe($"--path 3.1/runtime-deps/os/Dockerfile --path 3.1/runtime/os/Dockerfile");

                matrixInfo.Legs[1].Name.ShouldBe("5.0-noble-runtime-deps");
                imageBuilderPaths = matrixInfo.Legs[1].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                imageBuilderPaths.ShouldBe($"--path 3.1/runtime-deps/os/Dockerfile --path 5.0/runtime/os/Dockerfile");
            }
        }

        [TestMethod]
        [DataRow(MatrixType.PlatformVersionedOs)]
        [DataRow(MatrixType.PlatformDependencyGraph)]
        public async Task NonDependentReposWithSameProductVersion(MatrixType matrixType)
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = matrixType;
            command.Options.ProductVersionComponents = 2;

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                CreateDockerfile("1.0/repo1/os", tempFolderContext),
                                new string[] { "tag" })
                        },
                        productVersion: "1.0")),
                CreateRepo("repo2",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                CreateDockerfile("1.0/repo2/os", tempFolderContext),
                                new string[] { "tag" })
                        },
                        productVersion: "1.0"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();
            BuildMatrixInfo matrixInfo = matrixInfos.First();

            matrixInfo.Legs.Count.ShouldBe(2);
            matrixInfo.Legs[0].Name.ShouldBe(matrixType == MatrixType.PlatformDependencyGraph ? "1.0-repo1-os-Dockerfile" : "1.0-noble-repo1");
            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe($"--path 1.0/repo1/os/Dockerfile");

            matrixInfo.Legs[1].Name.ShouldBe(matrixType == MatrixType.PlatformDependencyGraph ? "1.0-repo2-os-Dockerfile" : "1.0-noble-repo2");
            imageBuilderPaths = matrixInfo.Legs[1].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe($"--path 1.0/repo2/os/Dockerfile");
        }

        [TestMethod]
        [DataRow(MatrixType.PlatformVersionedOs)]
        [DataRow(MatrixType.PlatformDependencyGraph)]
        public async Task DuplicatedPlatforms(MatrixType matrixType)
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = matrixType;
            command.Options.ProductVersionComponents = 2;

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("3.1/runtime/os", tempFolderContext),
                                new string[] { "tag" })
                        },
                        productVersion: "3.1"),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfileHelper.CreateDockerfile("3.1/runtime/os", tempFolderContext),
                                Array.Empty<string>())
                        },
                        productVersion: "3.1")));

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();

            matrixInfo.Legs.ShouldHaveSingleItem();

            string expectedLegName;
            if (matrixType == MatrixType.PlatformDependencyGraph)
            {
                expectedLegName = "3.1-runtime-os-Dockerfile";
            }
            else
            {
                expectedLegName = "3.1-noble-runtime";
            }

            matrixInfo.Legs[0].Name.ShouldBe(expectedLegName);

            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe($"--path 3.1/runtime/os/Dockerfile");
        }

        /// <summary>
        /// Scenario where a non-root Dockerfile has a duplicated platform that gets consolidated with another graph due
        /// to a shared root Dockerfile.
        /// </summary>
        [TestMethod]
        public async Task DuplicatedPlatforms_SubgraphConsolidation()
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.ProductVersionComponents = 2;

            string sharedRuntimeDepsPath;
            string sharedRuntimePath;

            Manifest manifest = CreateManifest(
                CreateRepo("runtime-deps",
                    CreateImage(
                        CreatePlatform(
                            sharedRuntimeDepsPath = CreateDockerfile("3.1/runtime-deps/os", tempFolderContext),
                            new string[] { "3.1-os" })),
                    CreateImage(
                        CreatePlatform(
                            sharedRuntimeDepsPath,
                            new string[] { "6.0-os" }))),
                CreateRepo("runtime",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("3.1/runtime/os", tempFolderContext, "runtime-deps:3.1-os"),
                            new string[] { "3.1-os" })),
                    CreateImage(
                        CreatePlatform(
                            sharedRuntimePath = CreateDockerfile("6.0/runtime/os", tempFolderContext, "runtime-deps:6.0-os"),
                            new string[] { "6.0-os" })),
                    CreateImage(
                        CreatePlatform(
                            sharedRuntimePath,
                            Array.Empty<string>()))));

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();

            matrixInfo.Legs.ShouldHaveSingleItem();

            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe($"--path 3.1/runtime-deps/os/Dockerfile --path 3.1/runtime/os/Dockerfile --path 6.0/runtime/os/Dockerfile");
        }

        /// <summary>
        /// Verifies that legs that have common Dockerfiles are consolidated together.
        /// </summary>
        [TestMethod]
        public async Task PlatformVersionedOs_ConsolidateCommonDockerfiles()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.ProductVersionComponents = 2;
            command.Options.CustomBuildLegGroups = new string[] { "pr-build" };

            Manifest manifest = CreateManifest(
                CreateRepo("repo",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/repo/os/amd64", tempFolderContext),
                            new string[] { "os" }, osVersion: "os")),
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/repo/os-variant/amd64", tempFolderContext),
                            new string[] { "os-variant" }, osVersion: "os-variant",
                            customBuildLegGroups: new[]
                            {
                                new CustomBuildLegGroup
                                {
                                    Type = CustomBuildLegDependencyType.Supplemental,
                                    Name = "pr-build",
                                    Dependencies = new string[] { "repo:os" }
                                }
                            })))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            matrixInfo.Legs.ShouldHaveSingleItem();
            BuildLegInfo leg = matrixInfo.Legs.First();
            leg.Name.ShouldBe("os-repo");
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

            imageBuilderPaths.ShouldBe("--path 1.0/repo/os/amd64/Dockerfile --path 1.0/repo/os-variant/amd64/Dockerfile");
        }

        /// <summary>
        /// This test verifies that we don't end up with a Dockerfile being built by multiple build jobs. In this scenario,
        /// the Dockerfile in danger of this is at the path 1.0/runtime-deps/mariner-distroless/amd64. This Dockerfile is
        /// shared by both a 1.0 and 2.0 version. In addition to a shared Dockerfile, the other key aspect of this scenario
        /// is the monitor images which have both a dependency on the full Mariner sdk as well as the distroless aspnet image.
        /// That multi-OS version dependency had exposed an issue with matrix generation that led to this test case being added.
        /// It was incorrectly separating out the 2.0 version from 1.0 but including the 1.0/runtime-deps/mariner-distroless/amd64
        /// in the 2.0 job. So both 1.0 and 2.0 had the same Dockerfile which leads to a conflict when publishing.
        /// </summary>
        [TestMethod]
        public async Task PlatformDependencyGraph_MultiVersionSharedDockerfileGraphWithDockerfileThatHasMultiOsVersionDependencies()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
            command.Options.ProductVersionComponents = 2;

            string sharedRuntimeDepsDockerfilePath;

            Manifest manifest = CreateManifest(
                CreateRepo("runtime-deps",
                    // 1.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime-deps/mariner/amd64", tempFolderContext),
                            new string[] { "1.0-mariner" }, osVersion: "mariner")),
                    // 1.0 distroless
                    CreateImage(
                        CreatePlatform(
                            sharedRuntimeDepsDockerfilePath = CreateDockerfile("1.0/runtime-deps/mariner-distroless/amd64", tempFolderContext),
                            new string[] { "1.0-mariner-distroless" }, osVersion: "mariner-distroless")),
                    // 2.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("2.0/runtime-deps/mariner/amd64", tempFolderContext),
                            new string[] { "2.0-mariner" }, osVersion: "mariner")),
                    // 2.0 distroless (shared Dockerfile with 1.0)
                    CreateImage(
                        CreatePlatform(
                            sharedRuntimeDepsDockerfilePath,
                            new string[] { "2.0-mariner-distroless" }, osVersion: "mariner-distroless"))),
                CreateRepo("runtime",
                    // 1.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/mariner/amd64", tempFolderContext, "runtime-deps:1.0-mariner"),
                            new string[] { "1.0-mariner" }, osVersion: "mariner")),
                    // 1.0 distroless
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/mariner-distroless/amd64", tempFolderContext, "runtime-deps:1.0-mariner-distroless"),
                            new string[] { "1.0-mariner-distroless" }, osVersion: "mariner-distroless")),
                    // 2.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("2.0/runtime/mariner/amd64", tempFolderContext, "runtime-deps:2.0-mariner"),
                            new string[] { "2.0-mariner" }, osVersion: "mariner")),
                    // 2.0 distroless
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("2.0/runtime/mariner-distroless/amd64", tempFolderContext, "runtime-deps:2.0-mariner-distroless"),
                            new string[] { "2.0-mariner-distroless" }, osVersion: "mariner-distroless"))),
                CreateRepo("aspnet",
                    // 1.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/aspnet/mariner/amd64", tempFolderContext, "runtime:1.0-mariner"),
                            new string[] { "1.0-mariner" }, osVersion: "mariner")),
                    // 1.0 distroless
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/aspnet/mariner-distroless/amd64", tempFolderContext, "runtime:1.0-mariner-distroless"),
                            new string[] { "1.0-mariner-distroless" }, osVersion: "mariner-distroless")),
                    // 2.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("2.0/aspnet/mariner/amd64", tempFolderContext, "runtime:2.0-mariner"),
                            new string[] { "2.0-mariner" }, osVersion: "mariner")),
                    // 2.0 distroless
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("2.0/aspnet/mariner-distroless/amd64", tempFolderContext, "runtime:2.0-mariner-distroless"),
                            new string[] { "2.0-mariner-distroless" }, osVersion: "mariner-distroless"))),
                CreateRepo("sdk",
                    // 1.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/sdk/mariner/amd64", tempFolderContext, "aspnet:1.0-mariner"),
                            new string[] { "1.0-mariner" }, osVersion: "mariner")),
                    // 2.0
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("2.0/sdk/mariner/amd64", tempFolderContext, "aspnet:2.0-mariner"),
                            new string[] { "2.0-mariner" }, osVersion: "mariner"))),
                CreateRepo("monitor",
                    // 1.0 distroless (based on 1.0 sdk and 1.0 aspnet distroless)
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/monitor/mariner-distroless/amd64", tempFolderContext, "sdk:1.0-mariner", "aspnet:1.0-mariner-distroless"),
                            new string[] { "1.0-mariner" }, osVersion: "mariner")),
                    // 2.0 distroless (based on 2.0 sdk and 2.0 aspnet distroless)
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("2.0/monitor/mariner-distroless/amd64", tempFolderContext, "sdk:2.0-mariner", "aspnet:2.0-mariner-distroless"),
                            new string[] { "2.0-mariner" }, osVersion: "mariner")))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
            matrixInfos.ShouldHaveSingleItem();

            BuildMatrixInfo matrix = matrixInfos.First();
            matrix.Legs.ShouldHaveSingleItem();

            BuildLegInfo leg = matrix.Legs.First();
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            imageBuilderPaths.ShouldBe("--path 1.0/runtime-deps/mariner/amd64/Dockerfile --path 1.0/runtime/mariner/amd64/Dockerfile --path 1.0/aspnet/mariner/amd64/Dockerfile --path 1.0/sdk/mariner/amd64/Dockerfile --path 1.0/monitor/mariner-distroless/amd64/Dockerfile --path 1.0/aspnet/mariner-distroless/amd64/Dockerfile --path 1.0/runtime/mariner-distroless/amd64/Dockerfile --path 1.0/runtime-deps/mariner-distroless/amd64/Dockerfile --path 2.0/runtime-deps/mariner/amd64/Dockerfile --path 2.0/runtime/mariner/amd64/Dockerfile --path 2.0/aspnet/mariner/amd64/Dockerfile --path 2.0/sdk/mariner/amd64/Dockerfile --path 2.0/monitor/mariner-distroless/amd64/Dockerfile --path 2.0/aspnet/mariner-distroless/amd64/Dockerfile --path 2.0/runtime/mariner-distroless/amd64/Dockerfile");
        }

        private static PlatformData CreateSimplePlatformData(string dockerfilePath, bool isCached = false)
        {
            PlatformData platform = Helpers.ImageInfoHelper.CreatePlatform(
                PathHelper.NormalizePath(dockerfilePath),
                simpleTags: new List<string>
                {
                    "tag"
                });
            platform.IsUnchanged = isCached;
            return platform;
        }

        /// <summary>
        /// Verifies that when a base image digest query fails with a non-404 error (e.g., authentication
        /// failure due to missing RegistryAuthentication config), the exception propagates rather than
        /// being silently swallowed. This is the fix for https://github.com/dotnet/docker-tools/issues/1964.
        /// </summary>
        [TestMethod]
        public async Task TrimCachedImages_DigestQueryAuthFailure_Throws()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            GenerateBuildMatrixCommand command = SetupTrimCacheTest(
                tempFolderContext,
                digestQueryThrows: true);

            command.LoadManifest();

            // The digest query throws a generic exception (simulating auth failure).
            // This should propagate instead of being silently swallowed.
            await Should.ThrowAsync<Exception>(command.GenerateMatrixInfoAsync);
        }

        /// <summary>
        /// Verifies that when a base image is not found in the registry (HTTP 404), it is treated as
        /// a cache miss and the platform is included in the matrix. This handles the case where a new
        /// image hasn't been published yet.
        /// </summary>
        [TestMethod]
        public async Task TrimCachedImages_DigestQueryReturnsNotFound_CacheMiss()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IManifestService> manifestServiceMock = new();
            manifestServiceMock
                .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ThrowsAsync(new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound));

            GenerateBuildMatrixCommand command = SetupTrimCacheTest(
                tempFolderContext,
                manifestServiceMock: manifestServiceMock);

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();

            // Image not found → cache miss → platform should be included in the matrix
            matrixInfos.ShouldHaveSingleItem();
            matrixInfos.First().Legs.ShouldHaveSingleItem();
        }

        /// <summary>
        /// Verifies that when the base image digest matches (auth is working and digest is unchanged),
        /// and the Dockerfile commit also matches, the platform is correctly cached and trimmed from
        /// the matrix.
        /// </summary>
        [TestMethod]
        public async Task TrimCachedImages_DigestAndCommitMatch_PlatformIsCached()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            const string baseImageDigestSha = "sha256:ea71a031ed91cd46b00d438876550bc765da43b4ae40f331a12daf62f0937758";

            // Create a mock that returns a matching digest for any image query
            Mock<IManifestService> manifestServiceMock = new();
            manifestServiceMock
                .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(baseImageDigestSha);

            GenerateBuildMatrixCommand command = SetupTrimCacheTest(
                tempFolderContext,
                manifestServiceMock: manifestServiceMock);

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();

            // Digest matches and Dockerfile unchanged → platform is cached → trimmed from matrix
            matrixInfos.ShouldBeEmpty();
        }

        /// <summary>
        /// Sets up a <see cref="GenerateBuildMatrixCommand"/> with a single platform that has an
        /// external base image, suitable for testing cache trimming behavior.
        /// </summary>
        private static GenerateBuildMatrixCommand SetupTrimCacheTest(
            TempFolderContext tempFolderContext,
            IEnumerable<ManifestServiceHelper.ImageDigestResults> externalImageDigestResults = null,
            bool digestQueryThrows = false,
            Mock<IManifestService> manifestServiceMock = null)
        {
            const string sourceRepoUrl = "https://github.com/dotnet/test";
            const string commitSha = "abc123def456";
            const string baseImageDigestSha = "sha256:ea71a031ed91cd46b00d438876550bc765da43b4ae40f331a12daf62f0937758";

            string runtimeDepsRelativeDir = "1.0/runtime-deps/os/amd64";
            string dockerfilePath = CreateDockerfile(runtimeDepsRelativeDir, tempFolderContext, "alpine:3.20");

            Manifest manifest = CreateManifest(
                CreateRepo("runtime-deps",
                    CreateImage(
                        CreatePlatform(dockerfilePath, ["tag"]))));

            string commitUrl = $"{sourceRepoUrl}/blob/{commitSha}/{PathHelper.NormalizePath(dockerfilePath)}";

            Mock<IGitService> gitServiceMock = new();
            gitServiceMock
                .Setup(o => o.GetCommitSha(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(commitSha);

            Mock<IManifestServiceFactory> manifestServiceFactoryMock;
            if (manifestServiceMock is not null)
            {
                manifestServiceFactoryMock = ManifestServiceHelper.CreateManifestServiceFactoryMock(manifestServiceMock);
            }
            else if (digestQueryThrows)
            {
                // Default mock throws a generic exception on all digest queries
                manifestServiceFactoryMock = ManifestServiceHelper.CreateManifestServiceFactoryMock();
            }
            else
            {
                manifestServiceFactoryMock = ManifestServiceHelper.CreateManifestServiceFactoryMock(
                    externalImageDigestResults: externalImageDigestResults ?? []);
            }

            ImageCacheService imageCacheService = new(
                Mock.Of<ILogger<ImageCacheService>>(),
                gitServiceMock.Object);

            GenerateBuildMatrixCommand command = new(
                TestHelper.CreateManifestJsonService(),
                CreateImageInfoService(),
                imageCacheService,
                manifestServiceFactoryMock.Object,
                Mock.Of<ILogger<GenerateBuildMatrixCommand>>());

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "imageinfo.json");
            command.Options.TrimCachedImages = true;
            command.Options.SourceRepoUrl = sourceRepoUrl;
            command.Options.SourceRepoPrefix = "mirror/";

            File.WriteAllText(
                Path.Combine(tempFolderContext.Path, command.Options.Manifest),
                JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime-deps",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(
                                        PathHelper.NormalizePath(dockerfilePath),
                                        baseImageDigest: $"library/alpine@{baseImageDigestSha}",
                                        commitUrl: commitUrl,
                                        simpleTags: ["tag"])
                                }
                            }
                        }
                    }
                }
            };
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            return command;
        }

        private static GenerateBuildMatrixCommand CreateCommand(IImageInfoService imageInfoService = null) =>
            new(
                TestHelper.CreateManifestJsonService(),
                imageInfoService ?? CreateImageInfoService(),
                Mock.Of<IImageCacheService>(),
                Mock.Of<IManifestServiceFactory>(),
                Mock.Of<ILogger<GenerateBuildMatrixCommand>>());

        private static ImageInfoService CreateImageInfoService() =>
            new(
                TestHelper.CreateManifestJsonService(),
                Mock.Of<IOrasServiceFactory>(),
                Mock.Of<ILogger<ImageInfoService>>());
    }
}
