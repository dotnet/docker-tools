// Licensed to the .NET Foundation under one or more agreements.
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
        [InlineData(null, "--path 2.2/runtime/os --path 2.1/runtime-deps/os", "2.2")]
        [InlineData("--path 2.2/runtime/os", "--path 2.2/runtime/os", "2.2")]
        [InlineData("--path 2.1/runtime-deps/os", "--path 2.1/runtime-deps/os", "2.1")]
        public void GenerateBuildMatrixCommand_PlatformVersionedOs(string filterPaths, string expectedPaths, string verificationLegName)
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.MatrixType = MatrixType.PlatformVersionedOs;
                if (filterPaths != null)
                {
                    command.Options.FilterOptions.Paths = filterPaths.Replace("--path ", "").Split(" ");
                }

                const string runtimeDepsRelativeDir = "2.1/runtime-deps/os";
                DirectoryInfo runtimeDepsDir = Directory.CreateDirectory(
                    Path.Combine(tempFolderContext.Path, runtimeDepsRelativeDir));
                string dockerfileRuntimeDepsFullPath = Path.Combine(runtimeDepsDir.FullName, "Dockerfile");
                File.WriteAllText(dockerfileRuntimeDepsFullPath, "FROM base");

                const string runtimeRelativeDir = "2.2/runtime/os";
                DirectoryInfo runtimeDir = Directory.CreateDirectory(
                    Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRuntimePath = Path.Combine(runtimeDir.FullName, "Dockerfile");
                File.WriteAllText(dockerfileRuntimePath, "FROM runtime-deps:tag");

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo("runtime-deps",
                        ManifestHelper.CreateImage(
                            new Platform[]
                            {
                                ManifestHelper.CreatePlatform(runtimeDepsRelativeDir, new string[] { "tag" })
                            },
                            productVersion: "2.1.1")),
                    ManifestHelper.CreateRepo("runtime",
                        ManifestHelper.CreateImage(
                            new Platform[]
                            {
                                ManifestHelper.CreatePlatform(runtimeRelativeDir, new string[] { "runtime" })
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

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo("runtime-deps",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(runtimeDepsRelativeDir, new string[] { "tag" }))),
                    ManifestHelper.CreateRepo("sdk",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(sdkRelativeDir, new string[] { "tag" }))),
                    ManifestHelper.CreateRepo("sample",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(sampleRelativeDir, new string[] { "tag" })))
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
    }
}
