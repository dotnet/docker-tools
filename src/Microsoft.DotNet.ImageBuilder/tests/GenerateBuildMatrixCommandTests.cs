// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GenerateBuildMatrixCommandTests
    {
        /// <summary>
        /// Verifies the platformVersionedOs matrix type.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/243
        /// </remarks>
        [Theory]
        [InlineData(null, "--path 2.1.1/runtime-deps/os --path 2.2/runtime/os", "2.1.1")]
        [InlineData("--path 2.2/runtime/os", "--path 2.2/runtime/os", "2.2")]
        [InlineData("--path 2.1.1/runtime-deps/os", "--path 2.1.1/runtime-deps/os", "2.1.1")]
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
                Assert.Single(matrixInfos);

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                BuildLegInfo leg = matrixInfo.Legs.First(leg => leg.Name.StartsWith(verificationLegName));
                string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                Assert.Equal(expectedPaths, imageBuilderPaths);
            }
        }

        /// <summary>
        /// Verifies the PlatformDependencyGraph matrix type.
        /// </summary>
        /// <remarks>
        /// Test uses a dependency graph of runtimedeps <- sdk <- sample
        /// </remarks>
        [Theory]
        [InlineData(null, "--path 1.0/runtimedeps/os/amd64 --path 1.0/sdk/os/amd64 --path 1.0/sample/os/amd64")]
        [InlineData("--path 1.0/runtimedeps/os/amd64", "--path 1.0/runtimedeps/os/amd64")]
        [InlineData("--path 1.0/runtimedeps/os/amd64 --path 1.0/sdk/os/amd64", "--path 1.0/runtimedeps/os/amd64 --path 1.0/sdk/os/amd64")]
        [InlineData("--path 1.0/sdk/os/amd64", "--path 1.0/sdk/os/amd64")]
        [InlineData("--path 1.0/sdk/os/amd64 --path 1.0/sample/os/amd64", "--path 1.0/sdk/os/amd64 --path 1.0/sample/os/amd64")]
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
                Assert.Single(matrixInfos);

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                BuildLegInfo leg = matrixInfo.Legs.First();
                string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                Assert.Equal(expectedPaths, imageBuilderPaths);
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

        [Theory]
        [InlineData(
            ImageCacheState.NotCached,
            ImageCacheState.NotCached,
            "--path 1.0/runtime/os/amd64/Dockerfile --path 1.0/sdk/os/amd64/Dockerfile",
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [InlineData(
            ImageCacheState.Cached,
            ImageCacheState.Cached,
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [InlineData(
            ImageCacheState.Cached,
            ImageCacheState.Cached,
            "--path 1.0/standalone/os/amd64/Dockerfile",
            "--path 2.0/standalone/os/amd64/Dockerfile",
            null,
            "*standalone*")]
        [InlineData(
            ImageCacheState.CachedWithMissingTags,
            ImageCacheState.Cached,
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [InlineData(
            ImageCacheState.Cached,
            ImageCacheState.NotCached,
            "--path 1.0/runtime/os/amd64/Dockerfile --path 1.0/sdk/os/amd64/Dockerfile",
            "--path 2.0/runtime/os/amd64/Dockerfile --path 2.0/sdk/os/amd64/Dockerfile")]
        [InlineData(
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
            string? leg2ExpectedPaths = null,
            string? leg3ExpectedPaths = null,
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

            GenerateBuildMatrixCommand command = new(imageCacheServiceMock.Object, Mock.Of<IManifestServiceFactory>(), Mock.Of<ILoggerService>());
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            BuildLegInfo leg = matrixInfo.Legs.First();
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal(leg1ExpectedPaths, imageBuilderPaths);

            if (leg2ExpectedPaths is not null)
            {
                leg = matrixInfo.Legs.Skip(1).First();
                imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal(leg2ExpectedPaths, imageBuilderPaths);
            }

            if (leg3ExpectedPaths is not null)
            {
                leg = matrixInfo.Legs.Skip(2).First();
                imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal(leg3ExpectedPaths, imageBuilderPaths);
            }
        }

        /// <summary>
        /// Verifies that the correct matrix is generated when the DistinctMatrixOsVersion option is set.
        /// </summary>
        [Fact]
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
            Assert.Equal(2, matrixInfos.Count());

            BuildMatrixInfo matrixInfo = matrixInfos.ElementAt(0);
            Assert.Equal($"{CustomMatrixOsVersion}Amd64", matrixInfo.Name);
            Assert.Single(matrixInfo.Legs);
            Assert.Equal(
                "--path 1.0/aspnet/os2/amd64/Dockerfile --path 1.0/sdk/os2/amd64/Dockerfile",
                matrixInfo.Legs.First().Variables.First(variable => variable.Name == "imageBuilderPaths").Value);

            matrixInfo = matrixInfos.ElementAt(1);
            Assert.Equal($"linuxAmd64", matrixInfo.Name);
            Assert.Single(matrixInfo.Legs);
            Assert.Equal(
                "--path 1.0/aspnet/os/amd64/Dockerfile --path 1.0/sdk/os/amd64/Dockerfile",
                matrixInfo.Legs.First().Variables.First(variable => variable.Name == "imageBuilderPaths").Value);
        }

        /// <summary>
        /// Verifies the platformVersionedOs matrix type is generated correctly when a
        /// custom build leg group is defined that has a parent graph.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/243
        /// </remarks>
        [Theory]
        [InlineData(CustomBuildLegDependencyType.Integral)]
        [InlineData(CustomBuildLegDependencyType.Supplemental)]
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
                                CreatePlatform(dockerfileRuntime2FullPath, new string[] { "tag" }, osVersion: "buster-slim")
                            },
                            productVersion: "2.0")),
                    CreateRepo("sdk2",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfileSdk2FullPath, new string[] { "tag" }, osVersion: "buster")
                            },
                            productVersion: "2.0"))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();
                Assert.Single(matrixInfos);

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                Assert.Single(matrixInfo.Legs);
                BuildLegInfo leg_1_0 = matrixInfo.Legs.First();
                string imageBuilderPaths = leg_1_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal("--path 1.0/runtime-deps/os/Dockerfile --path 1.0/runtime/os/Dockerfile --path 2.0/sdk/os2/Dockerfile --path 2.0/runtime/os2/Dockerfile", imageBuilderPaths);
                string osVersions = leg_1_0.Variables.First(variable => variable.Name == "osVersions").Value;
                Assert.Equal("--os-version focal --os-version buster --os-version buster-slim", osVersions);
            }
        }

        /// <summary>
        /// Verifies that a <see cref="MatrixType.PlatformDependencyGraph"/> build matrix can be generated correctly
        /// when there are both Windows Server Core and Nano Server platforms that have a custom build leg group
        /// dependency. The scenario for this is that, for a given matching version between Server Core and Nano Server,
        /// those Dockerfiles should be built in the same job.
        /// </summary>
        [Fact]
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            BuildLegInfo leg = matrixInfo.Legs.First();
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal(
                "--path 1.0/runtime/nanoserver-1909/Dockerfile --path 1.0/aspnet/nanoserver-1909/Dockerfile --path 1.0/runtime/windowsservercore-1909/Dockerfile --path 1.0/aspnet/windowsservercore-1909/Dockerfile",
                imageBuilderPaths);
            string osVersions = leg.Variables.First(variable => variable.Name == "osVersions").Value;
            Assert.Equal(
                "--os-version nanoserver-1909 --os-version windowsservercore-1909",
                osVersions);
        }

        /// <summary>
        /// Verifies the platformVersionedOs matrix type is generated correctly when there's a dependency on a
        /// tag that's outside the platform group.
        /// </summary>
        [Fact]
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
                            CreatePlatform(dockerfileRuntimeFullPath, new string[] { "tag" }, osVersion: "buster")
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            Assert.Equal(2, matrixInfo.Legs.Count);
            BuildLegInfo leg_1_0 = matrixInfo.Legs.First();
            string imageBuilderPaths = leg_1_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal("--path 1.0/runtime/os/Dockerfile", imageBuilderPaths);

            BuildLegInfo leg_2_0 = matrixInfo.Legs.ElementAt(1);
            imageBuilderPaths = leg_2_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal("--path 1.0/runtime2/os/Dockerfile --path 1.0/sdk3/os2/Dockerfile --path 1.0/runtime3/os2/Dockerfile", imageBuilderPaths);
        }

        /// <summary>
        /// Verifies that a <see cref="MatrixType.PlatformVersionedOs"/> build matrix can be generated correctly
        /// when there are multiple custom build leg groups defined.
        /// </summary>
        [Fact]
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
                                osVersion: "focal",
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
                                osVersion: "buster")
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            Assert.Equal(2, matrixInfo.Legs.Count());
            BuildLegInfo leg = matrixInfo.Legs.ElementAt(0);
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal(
                "--path 1.0/repo1/os/Dockerfile --path 1.0/repo2/os/Dockerfile",
                imageBuilderPaths);

            leg = matrixInfo.Legs.ElementAt(1);
            imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal(
                "--path 1.0/repo3/os/Dockerfile --path 1.0/repo4/os/Dockerfile",
                imageBuilderPaths);
        }

        /// <summary>
        /// Verifies the matrix produced by the platformVersionedOs matrix type for a scenario
        /// where the platforms in the image info are a result of cached images.
        /// </summary>
        [Theory]
        [InlineData(false, false, false, "--path 1.0/runtime/os/Dockerfile --path 1.0/aspnet/os/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [InlineData(true, false, false, "--path 1.0/aspnet/os/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [InlineData(true, true, false, "--path 1.0/sdk/os/Dockerfile")]
        [InlineData(true, true, true, null)]
        public async Task PlatformVersionedOs_Cached(bool isRuntimeCached, bool isAspnetCached, bool isSdkCached, string expectedPaths)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = CreateCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformVersionedOs;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "imageinfo.json");
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
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            IEnumerable<BuildMatrixInfo> matrixInfos = await command.GenerateMatrixInfoAsync();

            if (isRuntimeCached && isAspnetCached && isSdkCached)
            {
                Assert.Empty(matrixInfos);
            }
            else
            {
                Assert.Single(matrixInfos);
                Assert.Single(matrixInfos.First().Legs);

                BuildLegInfo buildLeg = matrixInfos.First().Legs.First();
                string imageBuilderPaths = buildLeg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                Assert.Equal(expectedPaths, imageBuilderPaths);
            }
        }

        /// <summary>
        /// Verifies that the platformVersionedOs matrix type doesn't create mutiple legs
        /// for a scenario where platforms in the same repo share a cached parent image.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/1141
        /// </remarks>
        [Theory]
        [InlineData(false, false, "--path 1.0/runtime/os/Dockerfile --path 1.0/aspnet/os-composite/Dockerfile --path 1.0/aspnet/os/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [InlineData(true, false, "--path 1.0/aspnet/os/Dockerfile --path 1.0/aspnet/os-composite/Dockerfile --path 1.0/sdk/os/Dockerfile")]
        [InlineData(true, true, "--path 1.0/aspnet/os-composite/Dockerfile --path 1.0/sdk/os/Dockerfile")]
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

            Assert.Single(matrixInfos);
            Assert.Single(matrixInfos.First().Legs);

            BuildLegInfo buildLeg = matrixInfos.First().Legs.First();
            string imageBuilderPaths = buildLeg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal(expectedPaths, imageBuilderPaths);
        }

        [Fact]
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            Assert.Single(matrixInfo.Legs);

            Assert.Equal("3.1-runtime-deps-os", matrixInfo.Legs[0].Name);
            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal($"--path {runtimeDepsRelativeDir}", imageBuilderPaths);
        }

        [Theory]
        [InlineData(MatrixType.PlatformVersionedOs)]
        [InlineData(MatrixType.PlatformDependencyGraph)]
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
            Assert.Single(matrixInfos);
            BuildMatrixInfo matrixInfo = matrixInfos.First();

            if (matrixType == MatrixType.PlatformDependencyGraph)
            {
                Assert.Single(matrixInfo.Legs);

                Assert.Equal("3.1-runtime-deps-os-Dockerfile-graph", matrixInfo.Legs[0].Name);
                string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal($"--path 3.1/runtime-deps/os/Dockerfile --path 3.1/runtime/os/Dockerfile --path 5.0/runtime/os/Dockerfile", imageBuilderPaths);
            }
            else
            {
                Assert.Equal(2, matrixInfo.Legs.Count);

                Assert.Equal("3.1-focal-core-runtime-deps", matrixInfo.Legs[0].Name);
                string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal($"--path 3.1/runtime-deps/os/Dockerfile --path 3.1/runtime/os/Dockerfile", imageBuilderPaths);

                Assert.Equal("5.0-focal-runtime-deps", matrixInfo.Legs[1].Name);
                imageBuilderPaths = matrixInfo.Legs[1].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal($"--path 3.1/runtime-deps/os/Dockerfile --path 5.0/runtime/os/Dockerfile", imageBuilderPaths);
            }
        }

        [Theory]
        [InlineData(MatrixType.PlatformVersionedOs)]
        [InlineData(MatrixType.PlatformDependencyGraph)]
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
            Assert.Single(matrixInfos);
            BuildMatrixInfo matrixInfo = matrixInfos.First();

            Assert.Equal(2, matrixInfo.Legs.Count);
            Assert.Equal(matrixType == MatrixType.PlatformDependencyGraph ? "1.0-repo1-os-Dockerfile" : "1.0-focal-repo1", matrixInfo.Legs[0].Name);
            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal($"--path 1.0/repo1/os/Dockerfile", imageBuilderPaths);

            Assert.Equal(matrixType == MatrixType.PlatformDependencyGraph ? "1.0-repo2-os-Dockerfile" : "1.0-focal-repo2", matrixInfo.Legs[1].Name);
            imageBuilderPaths = matrixInfo.Legs[1].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal($"--path 1.0/repo2/os/Dockerfile", imageBuilderPaths);
        }

        [Theory]
        [InlineData(MatrixType.PlatformVersionedOs)]
        [InlineData(MatrixType.PlatformDependencyGraph)]
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();

            Assert.Single(matrixInfo.Legs);

            string expectedLegName;
            if (matrixType == MatrixType.PlatformDependencyGraph)
            {
                expectedLegName = "3.1-runtime-os-Dockerfile";
            }
            else
            {
                expectedLegName = "3.1-focal-runtime";
            }

            Assert.Equal(expectedLegName, matrixInfo.Legs[0].Name);

            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal($"--path 3.1/runtime/os/Dockerfile", imageBuilderPaths);
        }

        /// <summary>
        /// Scenario where a non-root Dockerfile has a duplicated platform that gets consolidated with another graph due
        /// to a shared root Dockerfile.
        /// </summary>
        [Fact]
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();

            Assert.Single(matrixInfo.Legs);

            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal(
                $"--path 3.1/runtime-deps/os/Dockerfile --path 3.1/runtime/os/Dockerfile --path 6.0/runtime/os/Dockerfile",
                imageBuilderPaths);
        }

        /// <summary>
        /// Verifies that legs that have common Dockerfiles are consolidated together.
        /// </summary>
        [Fact]
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            Assert.Single(matrixInfo.Legs);
            BuildLegInfo leg = matrixInfo.Legs.First();
            Assert.Equal("os-repo", leg.Name);
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

            Assert.Equal("--path 1.0/repo/os/amd64/Dockerfile --path 1.0/repo/os-variant/amd64/Dockerfile", imageBuilderPaths);
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
        [Fact]
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
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrix = matrixInfos.First();
            Assert.Single(matrix.Legs);

            BuildLegInfo leg = matrix.Legs.First();
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal("--path 1.0/runtime-deps/mariner/amd64/Dockerfile --path 1.0/runtime/mariner/amd64/Dockerfile --path 1.0/aspnet/mariner/amd64/Dockerfile --path 1.0/sdk/mariner/amd64/Dockerfile --path 1.0/monitor/mariner-distroless/amd64/Dockerfile --path 1.0/aspnet/mariner-distroless/amd64/Dockerfile --path 1.0/runtime/mariner-distroless/amd64/Dockerfile --path 1.0/runtime-deps/mariner-distroless/amd64/Dockerfile --path 2.0/runtime-deps/mariner/amd64/Dockerfile --path 2.0/runtime/mariner/amd64/Dockerfile --path 2.0/aspnet/mariner/amd64/Dockerfile --path 2.0/sdk/mariner/amd64/Dockerfile --path 2.0/monitor/mariner-distroless/amd64/Dockerfile --path 2.0/aspnet/mariner-distroless/amd64/Dockerfile --path 2.0/runtime/mariner-distroless/amd64/Dockerfile",
                imageBuilderPaths);
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

        private static GenerateBuildMatrixCommand CreateCommand() =>
            new(Mock.Of<IImageCacheService>(), Mock.Of<IManifestServiceFactory>(), Mock.Of<ILoggerService>());
    }
}
