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
        /// Verifies the platformVersionedOs matrix type will include platform dependencies.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/docker-tools/issues/243
        /// </remarks>
        [Fact]
        public void GenerateBuildMatrixCommand_PlatformVersionedOs_IncludePlatformDependencies()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            using (TestHelper.SetWorkingDirectory(tempFolderContext.Path))
            {
                GenerateBuildMatrixCommand command = new GenerateBuildMatrixCommand();
                command.Options.Manifest = "manifest.json";
                command.Options.MatrixType = MatrixType.PlatformVersionedOs;

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
                            ManifestHelper.CreatePlatform(runtimeDepsRelativeDir, "tag"))),
                    ManifestHelper.CreateRepo("runtime",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(runtimeRelativeDir, "runtime")))
                );

                File.WriteAllText(command.Options.Manifest, JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                IEnumerable<BuildMatrixInfo> matrixInfos = command.GenerateMatrixInfo();
                Assert.Single(matrixInfos);

                BuildMatrixInfo matrixInfo = matrixInfos.First();
                BuildLegInfo leg = matrixInfo.Legs.First(leg => leg.Name.StartsWith("2.2"));
                string imageBuilderPaths = leg.Variables.First(variable => variable.Name == "imageBuilderPaths").Value;

                Assert.Equal("--path 2.2/runtime/os --path 2.1/runtime-deps/os", imageBuilderPaths);
            }
        }
    }
}
