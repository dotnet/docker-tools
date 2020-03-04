// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class BuildCommandTests
    {
        /// <summary>
        /// Verifies the command outputs an image info correctly for a basic scenario.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ImageInfoOutput_Basic()
        {
            const string repoName = "runtime";
            const string digest = "sha256:c74364a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd638c";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string baseImageDigest = $"{baseImageRepo}@sha256:d21234a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd1349";

            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
                dockerServiceMock
                    .SetupGet(o => o.Architecture)
                    .Returns(Architecture.AMD64);

                dockerServiceMock
                    .Setup(o => o.GetImageDigest($"{repoName}:{tag}", false))
                    .Returns(digest);

                dockerServiceMock
                    .Setup(o => o.GetImageDigest(baseImageTag, false))
                    .Returns(baseImageDigest);

                BuildCommand command = new BuildCommand(dockerServiceMock.Object);
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
                command.Options.IsPushEnabled = true;

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), $"FROM {baseImageTag}");

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo(repoName,
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { tag })))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                List<RepoData> repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = repoName,
                        Images = new SortedDictionary<string, ImageData>
                        {
                            {
                                $"{runtimeRelativeDir}/Dockerfile",
                                new ImageData
                                {
                                    Digest = digest,
                                    BaseImages = new SortedDictionary<string, string>
                                    {
                                        { baseImageTag, baseImageDigest }
                                    },
                                    SimpleTags = new List<string>
                                    {
                                        tag
                                    }
                                }
                            }
                        }
                    }
                };

                string expectedOutput = JsonHelper.SerializeObject(repos);
                string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

                Assert.Equal(expectedOutput, actualOutput);
            }
        }

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
