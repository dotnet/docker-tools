﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Newtonsoft.Json;
using Xunit;
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
        [InlineData(null, "--path 2.2/runtime/os --path 2.1.1/runtime-deps/os", "2.2")]
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
        /// Verifies the platformVersionedOs matrix type is generated correctly when a 
        /// custom build leg grouping is defined that has a parent graph.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/243
        /// </remarks>
        [Fact]
        public void GenerateBuildMatrixCommand_CustomBuildLegGroupingParentGraph()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                const string customBuildLegGrouping = "custom";
                GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.MatrixType = MatrixType.PlatformVersionedOs;
                command.Options.ProductVersionComponents = 2;
                command.Options.CustomBuildLegGrouping = customBuildLegGrouping;

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
                                    customBuildLegGroupings: new CustomBuildLegGrouping[]
                                    {
                                        new CustomBuildLegGrouping
                                        {
                                            Name = customBuildLegGrouping,
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
                BuildLegInfo leg_1_0 = matrixInfo.Legs.First();
                string imageBuilderPaths = leg_1_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal("--path 1.0/runtime-deps/os/Dockerfile --path 1.0/runtime/os/Dockerfile --path 2.0/sdk/os2/Dockerfile --path 2.0/runtime/os2/Dockerfile", imageBuilderPaths);
                string osVersions = leg_1_0.Variables.First(variable => variable.Name == "osVersions").Value;
                Assert.Equal("--os-version disco --os-version buster", osVersions);

                BuildLegInfo leg_2_0 = matrixInfo.Legs.ElementAt(1);
                imageBuilderPaths = leg_2_0.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;
                Assert.Equal("--path 2.0/runtime/os2/Dockerfile --path 2.0/sdk/os2/Dockerfile", imageBuilderPaths);
                osVersions = leg_2_0.Variables.First(variable => variable.Name == "osVersions").Value;
                Assert.Equal("--os-version buster", osVersions);
            }
        }

        /// <summary>
        /// Verifies that a <see cref="MatrixType.PlatformDependencyGraph"/> build matrix can be generated correctly
        /// when there are both Windows Server Core and Nano Server platforms that have a custom build leg grouping
        /// dependency. The scenario for this is that, for a given matching version between Server Core and Nano Server,
        /// those Dockerfiles should be built in the same job.
        /// </summary>
        [Fact]
        public void GenerateBuildMatrixCommand_ServerCoreAndNanoServerDependency()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            const string customBuildLegGrouping = "custom";
            GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.MatrixType = MatrixType.PlatformDependencyGraph;
            command.Options.ProductVersionComponents = 2;
            command.Options.CustomBuildLegGrouping = customBuildLegGrouping;

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
                                customBuildLegGroupings: new CustomBuildLegGrouping[]
                                {
                                    new CustomBuildLegGrouping
                                    {
                                        Name = customBuildLegGrouping,
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
    }
}
