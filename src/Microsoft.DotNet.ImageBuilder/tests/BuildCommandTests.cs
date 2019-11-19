// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class BuildCommandTests
    {
        /// <summary>
        /// Verifies the command outputs an image info correctly when the manifest references a custom named Dockerfile.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ImageInfoOutput_CustomDockerfile()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
                dockerServiceMock
                    .SetupGet(o => o.Architecture)
                    .Returns(Architecture.AMD64);

                BuildCommand command = new BuildCommand(dockerServiceMock.Object);
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo("runtime",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "runtime" })))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                RepoData[] repos = JsonConvert.DeserializeObject<RepoData[]>(File.ReadAllText(command.Options.ImageInfoOutputPath));
                Assert.Equal(PathHelper.NormalizePath(dockerfileRelativePath), repos[0].Images.First().Key);
            }
        }
    }
}
