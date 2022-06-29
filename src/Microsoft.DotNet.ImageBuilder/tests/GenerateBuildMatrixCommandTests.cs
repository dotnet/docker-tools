// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
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
        public void GenerateBuildMatrixCommand_PlatformVersionedOs(string filterPaths, string expectedPaths, string verificationLegName)
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.MatrixType = MatrixType.PlatformVersionedOs;
                command.Options.ProductVersionComponents = 3;
                if (filterPaths != null)
                {
                    command.Options.FilterOptions.Paths = filterPaths.Replace("--path ", "").Split(" ");
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
                IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void GenerateBuildMatrixCommand_PlatformDependencyGraph(string filterPaths, string expectedPaths)
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
                if (filterPaths != null)
                {
                    command.Options.FilterOptions.Paths = filterPaths.Replace("--path ", "").Split(" ");
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
                IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
                Assert.Single(matrixInfos);

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                BuildLegInfo leg = matrixInfo.Legs.First();
                string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                Assert.Equal(expectedPaths, imageBuilderPaths);
            }
        }

        /// <summary>
        /// Verifies that the correct matrix is generated when the DistinctMatrixOsVersion option is set.
        /// </summary>
        [Fact]
        public void GenerateBuildMatrixCommand_CustomMatrixOsVersion()
        {
            const string CustomMatrixOsVersion = "custom";
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void GenerateBuildMatrixCommand_CustomBuildLegGroupingParentGraph(CustomBuildLegDependencyType dependencyType)
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                const string customBuildLegGroup = "custom";
                GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
                IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void GenerateBuildMatrixCommand_ServerCoreAndNanoServerDependency()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            const string customBuildLegGroup = "custom";
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void GenerateBuildMatrixCommand_ParentGraphOutsidePlatformGroup()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void GenerateBuildMatrixCommand_MultiBuildLegGroups()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            const string customBuildLegGroup1 = "custom1";
            const string customBuildLegGroup2 = "custom2";
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void PlatformVersionedOs_Cached(bool isRuntimeCached, bool isAspnetCached, bool isSdkCached, string expectedPaths)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();

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

        [Fact]
        public void PlatformDependencyGraph_CrossReferencedDockerfileFromMultipleRepos_SingleDockerfile()
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            Assert.Single(matrixInfo.Legs);

            Assert.Equal("3.1-runtime-deps-os-graph", matrixInfo.Legs[0].Name);
            string imageBuilderPaths = matrixInfo.Legs[0].Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
            Assert.Equal($"--path {runtimeDepsRelativeDir}", imageBuilderPaths);
        }

        [Theory]
        [InlineData(MatrixType.PlatformVersionedOs)]
        [InlineData(MatrixType.PlatformDependencyGraph)]
        public void CrossReferencedDockerfileFromMultipleRepos_ImageGraph(MatrixType matrixType)
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void NonDependentReposWithSameProductVersion(MatrixType matrixType)
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
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
        public void DuplicatedPlatforms(MatrixType matrixType)
        {
            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();

            Assert.Single(matrixInfo.Legs);

            string expectedLegName;
            if (matrixType == MatrixType.PlatformDependencyGraph)
            {
                expectedLegName = "3.1-runtime-os-Dockerfile-graph";
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
        /// Verifies that legs that have common Dockerfiles are consolidated together.
        /// </summary>
        [Fact]
        public void PlatformVersionedOs_ConsolidateCommonDockerfiles()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateBuildMatrixCommand command = new();
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
            IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
            Assert.Single(matrixInfos);

            BuildMatrixInfo matrixInfo = matrixInfos.First();
            Assert.Single(matrixInfo.Legs);
            BuildLegInfo leg = matrixInfo.Legs.First();
            Assert.Equal("os-repo", leg.Name);
            string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

            Assert.Equal("--path 1.0/repo/os/amd64/Dockerfile --path 1.0/repo/os-variant/amd64/Dockerfile", imageBuilderPaths);
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
    }
}
