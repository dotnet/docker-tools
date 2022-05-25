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
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class BuildCommandTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public BuildCommandTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        /// <summary>
        /// Verifies the command outputs an image info correctly for a basic scenario.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ImageInfoOutput_Basic()
        {
            const string getInstalledPackagesScriptPath = "get-pkgs.sh";
            const string runtimeDepsRepo = "runtime-deps";
            const string runtimeRepo = "runtime";
            const string aspnetRepo = "aspnet";
            string runtimeDepsDigest = $"{runtimeDepsRepo}@sha256:c74364a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd638c";
            string runtimeDigest = $"{runtimeRepo}@sha256:adc914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed99227";
            string aspnetDigest = $"{aspnetRepo}@sha256:781914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed0045a";
            IEnumerable<string> runtimeDepsLayers = new [] { 
                "sha256:777b2c648970480f50f5b4d0af8f9a8ea798eea43dbcf40ce4a8c7118736bdcf",
                "sha256:b9dfc8eed8d66f1eae8ffe46be9a26fe047a7f6554e9dbc2df9da211e59b4786" };
            IEnumerable<string> runtimeLayers = 
                runtimeDepsLayers.Concat(new [] { "sha256:466982335a8bacfe63b8f75a2e8c6484dfa7f7e92197550643b3c1457fa445b4" });
            IEnumerable<string> aspnetLayers = 
                runtimeLayers.Concat(new [] { "sha256:d305fbfc4bd0d9f38662e979dced9831e3b5e4d85442397a8ec0a0e7bcf5458b"});
            const string tag = "tag";
            const string localTag = "localtag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string baseImageDigest = $"{baseImageRepo}@sha256:d21234a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd1349";

            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(runtimeDepsDigest);

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{runtimeRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(runtimeDigest);

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{aspnetRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(aspnetDigest);

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(baseImageDigest);

                dockerServiceMock
                    .Setup(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(runtimeDepsLayers);

                dockerServiceMock
                    .Setup(o => o.GetImageManifestLayersAsync($"{runtimeRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(runtimeLayers);

                dockerServiceMock
                    .Setup(o => o.GetImageManifestLayersAsync($"{aspnetRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(aspnetLayers);

                string[] runtimeDepsInstalledPackages =
                {
                    "DEB,pkg1=1.0.0"
                };

                string[] runtimeInstalledPackages =
                {
                    "DEB,pkg1=1.0.0",
                    "DEB,pkg2=1.1.0"
                };

                string[] aspnetInstalledPackages =
                {
                    "DEB,pkg1=1.0.0",
                    "DEB,pkg2=1.1.0",
                    "DEB,pkg3=2.0.0",
                };

                Mock<IProcessService> processServiceMock = new();

                processServiceMock
                    .Setup(o => o.Execute(It.IsAny<string>(), It.Is<string>(val => val.Contains(getInstalledPackagesScriptPath) && val.Contains($"{runtimeDepsRepo}:{tag}")), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(string.Join(Environment.NewLine, runtimeDepsInstalledPackages));

                processServiceMock
                    .Setup(o => o.Execute(It.IsAny<string>(), It.Is<string>(val => val.Contains(getInstalledPackagesScriptPath) && val.Contains($"{runtimeRepo}:{tag}")), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(string.Join(Environment.NewLine, runtimeInstalledPackages));

                processServiceMock
                    .Setup(o => o.Execute(It.IsAny<string>(), It.Is<string>(val => val.Contains(getInstalledPackagesScriptPath) && val.Contains($"{aspnetRepo}:{tag}")), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(string.Join(Environment.NewLine, aspnetInstalledPackages));

                DateTime createdDate = DateTime.Now;

                dockerServiceMock
                    .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{tag}", false))
                    .Returns(createdDate);
                dockerServiceMock
                    .Setup(o => o.GetCreatedDate($"{runtimeRepo}:{tag}", false))
                    .Returns(createdDate);
                dockerServiceMock
                    .Setup(o => o.GetCreatedDate($"{aspnetRepo}:{tag}", false))
                    .Returns(createdDate);

                string runtimeDepsDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                    "1.0/runtime-deps/os", tempFolderContext, baseImageTag);

                string runtimeDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                    "1.0/runtime/os", tempFolderContext, $"{runtimeDepsRepo}:{tag}");

                string aspnetDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                    "1.0/aspnet/os", tempFolderContext, $"{runtimeRepo}:{localTag}");

                const string dockerfileCommitSha = "mycommit";
                Mock<IGitService> gitServiceMock = new();
                gitServiceMock
                    .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)), It.IsAny<bool>()))
                    .Returns(dockerfileCommitSha);
                gitServiceMock
                    .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDockerfileRelativePath)), It.IsAny<bool>()))
                    .Returns(dockerfileCommitSha);
                gitServiceMock
                    .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, aspnetDockerfileRelativePath)), It.IsAny<bool>()))
                    .Returns(dockerfileCommitSha);

                BuildCommand command = new(dockerServiceMock.Object, Mock.Of<ILoggerService>(), gitServiceMock.Object, processServiceMock.Object);
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
                command.Options.IsPushEnabled = true;
                command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

                const string ProductVersion = "1.0.1";

                Platform runtimePlatform = CreatePlatform(runtimeDockerfileRelativePath, new string[] { tag });
                runtimePlatform.Tags.Add(localTag, new Tag
                {
                    IsLocal = true
                });

                Manifest manifest = CreateManifest(
                    CreateRepo(runtimeDepsRepo,
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag })
                            },
                            new Dictionary<string, Tag>
                            {
                                { "shared", new Tag() }
                            },
                            ProductVersion)),
                    CreateRepo(runtimeRepo,
                        CreateImage(
                            new Platform[]
                            {
                                runtimePlatform
                            },
                            productVersion: ProductVersion)),
                    CreateRepo(aspnetRepo,
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(aspnetDockerfileRelativePath, new string[] { tag })
                            },
                            productVersion: ProductVersion))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                ImageArtifactDetails imageArtifactDetails = new()
                {
                    Repos =
                    {
                        new RepoData
                        {
                            Repo = runtimeDepsRepo,
                            Images =
                            {
                                new ImageData
                                {
                                    ProductVersion = ProductVersion,
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Dockerfile = runtimeDepsDockerfileRelativePath,
                                            Architecture = "amd64",
                                            OsType = "Linux",
                                            OsVersion = "focal",
                                            Digest = runtimeDepsDigest,
                                            BaseImageDigest = baseImageDigest,
                                            Layers = runtimeDepsLayers.ToList(),
                                            Created = createdDate.ToUniversalTime(),
                                            CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                            SimpleTags =
                                            {
                                                tag
                                            },
                                        }
                                    },
                                    Manifest = new ManifestData
                                    {
                                        SharedTags =
                                        {
                                            "shared"
                                        }
                                    }
                                }
                            }
                        },
                        new RepoData
                        {
                            Repo = runtimeRepo,
                            Images =
                            {
                                new ImageData
                                {
                                    ProductVersion = ProductVersion,
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Dockerfile = runtimeDockerfileRelativePath,
                                            Architecture = "amd64",
                                            OsType = "Linux",
                                            OsVersion = "focal",
                                            Digest = runtimeDigest,
                                            BaseImageDigest = runtimeDepsDigest,
                                            Layers = runtimeLayers.ToList(),
                                            Created = createdDate.ToUniversalTime(),
                                            CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDockerfileRelativePath}",
                                            SimpleTags =
                                            {
                                                tag
                                            },
                                        }
                                    }
                                }
                            }
                        },
                        new RepoData
                        {
                            Repo = aspnetRepo,
                            Images =
                            {
                                new ImageData
                                {
                                    ProductVersion = ProductVersion,
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Dockerfile = aspnetDockerfileRelativePath,
                                            Architecture = "amd64",
                                            OsType = "Linux",
                                            OsVersion = "focal",
                                            Digest = aspnetDigest,
                                            BaseImageDigest = runtimeDigest,
                                            Layers = aspnetLayers.ToList(),
                                            Created = createdDate.ToUniversalTime(),
                                            CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{aspnetDockerfileRelativePath}",
                                            SimpleTags =
                                            {
                                                tag
                                            },
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                string expectedOutput = JsonHelper.SerializeObject(imageArtifactDetails);
                string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

                Assert.Equal(expectedOutput, actualOutput);
            }
        }

        /// <summary>
        /// Verifies the command outputs an image info correctly for a scenario where a platform has been duplicated in order
        /// for it to be contained in two different images.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ImageInfoOutput_DuplicatedPlatform()
        {
            const string runtimeRepo = "runtime";
            string runtimeDigest = $"{runtimeRepo}@sha256:adc914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed99227";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string baseImageDigest = $"{baseImageRepo}@sha256:d21234a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd1349";

            TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(baseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeRepo}:{tag}", false))
                .Returns(createdDate);

            string runtimeDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime/os", tempFolderContext, baseImageTag);

            const string dockerfileCommitSha = "mycommit";
            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(dockerfileCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.1";

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDockerfileRelativePath, new string[] { tag })
                        },
                        productVersion: ProductVersion),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDockerfileRelativePath, new string[0])
                        },
                        new Dictionary<string, Tag>
                        {
                            { "shared", new Tag() }
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = runtimeRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDigest,
                                        BaseImageDigest = baseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDockerfileRelativePath}"
                                    }
                                }
                            },
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                },
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDigest,
                                        BaseImageDigest = baseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDockerfileRelativePath}"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(imageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);
        }

        /// <summary>
        /// Verifies the tags that get built and published.
        /// </summary>
        [Fact]
        public async Task BuildCommand_Publish()
        {
            const string repoName = "runtime";
            const string tag = "tag";
            const string localTag = "localtag";
            const string sharedTag = "shared";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();
            dockerServiceMock
                .Setup(o => o.GetImageArch(baseImageTag, false))
                .Returns((Architecture.ARM, "v7"));

            BuildCommand command = new(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IGitService>(), Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.IsPushEnabled = true;

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
            string dockerfileAbsolutePath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(dockerfileAbsolutePath, $"FROM {baseImageTag}");

            Platform platform = CreatePlatform(dockerfileRelativePath, new string[] { tag }, architecture: Architecture.ARM, variant: "v7");
            platform.Tags.Add(localTag, new Tag { IsLocal = true });

            Manifest manifest = CreateManifest(
                CreateRepo(repoName,
                    CreateImage(
                        new Platform[]
                        {
                            platform
                        },
                        new Dictionary<string, Tag>
                        {
                            { sharedTag, new Tag() }
                        }))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(
                o => o.BuildImage(
                    dockerfileAbsolutePath,
                    PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeRelativeDir)),
                    "linux/arm/v7",
                    new string[]
                    {
                        TagInfo.GetFullyQualifiedName(repoName, tag),
                        TagInfo.GetFullyQualifiedName(repoName, localTag),
                        TagInfo.GetFullyQualifiedName(repoName, sharedTag)
                    },
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()));

            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, tag), It.IsAny<bool>()));
            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, sharedTag), It.IsAny<bool>()));
            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, localTag), It.IsAny<bool>()), Times.Never);
        }

        /// <summary>
        /// Verifies that the manifest's platform architecture settings match the architecture of the base image.
        /// </summary>
        [Fact]
        public async Task BuildCommand_VerifyOnBaseImageArchMismatch()
        {
            const string repoName = "runtime";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            BuildCommand command = new(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IGitService>(), Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
            string dockerfileAbsolutePath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(dockerfileAbsolutePath, $"FROM {baseImageTag}");

            Platform platform = CreatePlatform(dockerfileRelativePath, new string[] { tag }, architecture: Architecture.ARM, variant: "v7");

            Manifest manifest = CreateManifest(
                CreateRepo(repoName,
                    CreateImage(
                        new Platform[]
                        {
                            platform
                        }))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());
            Assert.StartsWith(
                $"Platform '{PathHelper.NormalizePath(dockerfileRelativePath)}' is configured with an architecture that is not compatible with the base image '{baseImageTag}'",
                ex.Message);
        }

        /// <summary>
        /// Verifies that manifest-defined and globally-defined build args can be used.
        /// </summary>
        [Fact]
        public async Task BuildCommand_BuildArgs()
        {
            const string repoName = "runtime";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            BuildCommand command = new(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IGitService>(), Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.BuildArgs.Add("arg1", "val1");
            command.Options.BuildArgs.Add("arg2", "val2a");

            Platform platform = CreatePlatform(
                DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext, baseImageTag),
                new string[] { tag });
            platform.BuildArgs.Add("arg2", "val2b");
            platform.BuildArgs.Add("arg3", "val3");

            Manifest manifest = CreateManifest(
                CreateRepo(repoName,
                    CreateImage(
                        new Platform[]
                        {
                            platform
                        }))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(
                o => o.BuildImage(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.Is<Dictionary<string, string>>(
                        args => args.Count == 3 && args["arg1"] == "val1" && args["arg2"] == "val2b" && args["arg3"] == "val3"),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()));
        }

        /// <summary>
        /// Verifies that an image with no base image will get built.
        /// </summary>
        [Fact]
        public async Task BuildCommand_NoBaseImage_Build()
        {
            const string repoName = "runtime";
            const string tag = "tag";
            const string localTag = "localtag";
            const string sharedTag = "shared";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            BuildCommand command = new BuildCommand(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IGitService>(), Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.IsPushEnabled = true;

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
            string dockerfileAbsolutePath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(dockerfileAbsolutePath, $"FROM scratch");

            Platform platform = CreatePlatform(dockerfileRelativePath, new string[] { tag });
            platform.Tags.Add(localTag, new Tag { IsLocal = true });

            Manifest manifest = CreateManifest(
                CreateRepo(repoName,
                    CreateImage(
                        new Platform[]
                        {
                            platform
                        },
                        new Dictionary<string, Tag>
                        {
                            { sharedTag, new Tag() }
                        }))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(
                o => o.BuildImage(
                    dockerfileAbsolutePath,
                    PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeRelativeDir)),
                    "linux/amd64",
                    new string[]
                    {
                        TagInfo.GetFullyQualifiedName(repoName, tag),
                        TagInfo.GetFullyQualifiedName(repoName, localTag),
                        TagInfo.GetFullyQualifiedName(repoName, sharedTag)
                    },
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()));

            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, tag), It.IsAny<bool>()));
            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, sharedTag), It.IsAny<bool>()));
            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, localTag), It.IsAny<bool>()), Times.Never);
        }

        /// <summary>
        /// Verifies that an image with no base image will be considered up-to-date.
        /// </summary>
        [Fact]
        public async Task BuildCommand_NoBaseImage_Cached()
        {
            const string runtimeDepsRepo = "runtime-deps";
            string runtimeDepsDigest = $"{runtimeDepsRepo}@sha1";
            const string tag = "tag";
            const string newTag = "new-tag";
            const string currentRuntimeDepsCommitSha = "commit-sha";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{newTag}", false))
                .Returns(createdDate);

            string runtimeDepsLinuxDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/linux", tempFolderContext, "scratch");

            Mock<IGitService> gitServiceMock = new();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsLinuxDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);

            BuildCommand command = new(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.0";

            List<RepoData> sourceRepos = new()
            {
                new RepoData
                {
                    Repo = runtimeDepsRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = runtimeDepsDigest,
                                    Created = createdDate.ToUniversalTime(),
                                    SimpleTags =
                                    {
                                        tag
                                    },
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}"
                                }
                            },
                            Manifest = new ManifestData
                            {
                                SharedTags = new List<string>
                                {
                                    "shared"
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails sourceImageArtifactDetails = new()
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsLinuxDockerfileRelativePath, new string[] { tag, newTag })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "shared", new Tag() }
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new()
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            newTag,
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}"
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.PullImage(runtimeDepsDigest, null, false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDepsRepo}:{tag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDepsRepo}:{newTag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDepsRepo}:shared", false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{newTag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{newTag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:shared", false));
            dockerServiceMock.Verify(o => o.GetCreatedDate(It.IsAny<string>(), false));
            dockerServiceMock.Verify(o =>
                o.BuildImage(
                    PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsLinuxDockerfileRelativePath)),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never);

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies the command outputs an image info correctly when the manifest references a custom named Dockerfile.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ImageInfoOutput_CustomDockerfile()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();
                const string digest = "runtime@sha256:c74364a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd638c";
                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync("runtime:runtime", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(digest);

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

                const string dockerfileCommitSha = "mycommit";
                Mock<IGitService> gitServiceMock = new Mock<IGitService>();
                gitServiceMock
                    .Setup(o => o.GetCommitSha(dockerfileRelativePath, It.IsAny<bool>()))
                    .Returns(dockerfileCommitSha);

                BuildCommand command = new BuildCommand(dockerServiceMock.Object, Mock.Of<ILoggerService>(), gitServiceMock.Object, Mock.Of<IProcessService>());
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
                command.Options.SourceRepoUrl = "https://source";

                Manifest manifest = CreateManifest(
                    CreateRepo("runtime",
                        CreateImage(
                            CreatePlatform(dockerfileRelativePath, new string[] { "runtime" })))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                ImageArtifactDetails imageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(
                    File.ReadAllText(command.Options.ImageInfoOutputPath));
                Assert.Equal(
                    PathHelper.NormalizePath(dockerfileRelativePath),
                    imageArtifactDetails.Repos[0].Images.First().Platforms.First().Dockerfile);
            }
        }

        /// <summary>
        /// Verifies an exception is thrown if the build output contains pull information.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BuildCommand_ThrowsIfImageIsPulled(bool isSkipPullingEnabled)
        {
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock("Pulling from");

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            
            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = PathHelper.NormalizePath(Path.Combine(runtimeRelativeDir, "Dockerfile"));
            string fullDockerfilePath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(fullDockerfilePath, $"FROM baserepo:basetag");

            BuildCommand command = new BuildCommand(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IGitService>(), Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.IsSkipPullingEnabled = isSkipPullingEnabled;

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                             CreatePlatform(dockerfileRelativePath, new string[] { "tag" })
                        },
                        new Dictionary<string, Tag>
                        {
                             { "shared", new Tag() }
                        },
                        "1.0.1"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();

            if (isSkipPullingEnabled)
            {
                await command.ExecuteAsync();
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(command.ExecuteAsync);
            }
        }

        /// <summary>
        /// Verifies the image caching logic.
        /// </summary>
        [Theory]
        [InlineData(
            "All images cached",
            "sha256:baseImageSha-1", "sha256:baseImageSha-1",
            "sha256:runtimeDepsImageSha-1", "sha256:runtimeDepsImageSha-1",
            "runtimeDepsCommitSha-1", "runtimeDepsCommitSha-1",
            "runtimeCommitSha-1", "runtimeCommitSha-1",
            true, true)]
        [InlineData(
            "All previously published, diffs for all image digests and commit SHAs",
            "sha256:baseImageSha-1", "sha256:baseImageSha-2",
            "sha256:runtimeDepsImageSha-1", "sha256:runtimeDepsImageSha-2",
            "runtimeDepsCommitSha-1", "runtimeDepsCommitSha-2",
            "runtimeCommitSha-1", "runtimeCommitSha-2",
            false, false)]
        [InlineData(
            "All previously published, diff for runtimeDeps image digest",
            "sha256:baseImageSha-1", "sha256:baseImageSha-1",
            "sha256:runtimeDepsImageSha-1", "sha256:runtimeDepsImageSha-2",
            "runtimeDepsCommitSha-1", "runtimeDepsCommitSha-1",
            "runtimeCommitSha-1", "runtimeCommitSha-1",
            true, false)]
        [InlineData(
            "All previously published, diff for runtime commit SHA",
            "sha256:baseImageSha-1", "sha256:baseImageSha-1",
            "sha256:runtimeDepsImageSha-1", "sha256:runtimeDepsImageSha-1",
            "runtimeDepsCommitSha-1", "runtimeDepsCommitSha-1",
            "runtimeCommitSha-1", "runtimeCommitSha-2",
            true, false)]
        [InlineData(
            "All previously published, diff for base image digest",
            "sha256:baseImageSha-1", "sha256:baseImageSha-2",
            "sha256:runtimeDepsImageSha-1", "sha256:runtimeDepsImageSha-2",
            "runtimeDepsCommitSha-1", "runtimeDepsCommitSha-1",
            "runtimeCommitSha-1", "runtimeCommitSha-1",
            false, false)]
        [InlineData(
            "Runtime not previously published",
            "sha256:baseImageSha-1", "sha256:baseImageSha-1",
            "sha256:runtimeDepsImageSha-1", "sha256:runtimeDepsImageSha-1",
            "runtimeDepsCommitSha-1", "runtimeDepsCommitSha-1",
            null, "runtimeCommitSha-1",
            true, false)]
        [InlineData(
            "No images previously published",
            "sha256:baseImageSha-1", "sha256:baseImageSha-1",
            null, "sha256:runtimeDepsImageSha-1",
            null, "runtimeDepsCommitSha-1",
            null, "runtimeCommitSha-1",
            false, false)]
        public async Task BuildCommand_Caching(
            string scenario,
            string sourceBaseImageSha,
            string currentBaseImageSha,
            string sourceRuntimeDepsImageSha,
            string currentRuntimeDepsImageSha,
            string sourceRuntimeDepsCommitSha,
            string currentRuntimeDepsCommitSha,
            string sourceRuntimeCommitSha,
            string currentRuntimeCommitSha,
            bool isRuntimeDepsCached,
            bool isRuntimeCached)
        {
            _outputHelper.WriteLine($"Running scenario '{scenario}'");

            const string registry = "mcr.microsoft.com";
            const string registryOverride = "new-registry.azurecr.io";
            const string repoPrefixOverride = "test/";
            string overridePrefix = $"{registryOverride}/{repoPrefixOverride}";

            const string runtimeDepsRepo = "runtime-deps";
            const string runtimeRepo = "runtime";
            string runtimeDepsRepoQualified = $"{registry}/{runtimeDepsRepo}";
            string runtimeRepoQualified = $"{registry}/{runtimeRepo}";
            string runtimeDepsDigest = $"{runtimeDepsRepoQualified}@{currentRuntimeDepsImageSha}";
            string runtimeDigest = $"{runtimeRepoQualified}@sha256:adc914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed99227";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string runtimeDepsBaseImageDigest = $"{baseImageRepo}@{currentBaseImageSha}";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepoQualified}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{overridePrefix}{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeRepoQualified}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDigest);

            dockerServiceMock
               .Setup(o => o.GetImageDigestAsync($"{overridePrefix}{runtimeRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
               .ReturnsAsync(runtimeDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsBaseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{overridePrefix}{runtimeDepsRepo}:{tag}", false))
                .Returns(createdDate);
            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{overridePrefix}{runtimeRepo}:{tag}", false))
                .Returns(createdDate);

            string runtimeDepsDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/os", tempFolderContext, baseImageTag);

            string runtimeDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime/os", tempFolderContext, $"{runtimeDepsRepoQualified}:{tag}");

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";
            command.Options.RegistryOverride = registryOverride;
            command.Options.RepoPrefix = repoPrefixOverride;

            const string ProductVersion = "1.0.1";

            List<RepoData> sourceRepos = new List<RepoData>();
            if (sourceBaseImageSha != null)
            {
                sourceRepos.Add(
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsDigest,
                                        BaseImageDigest = $"{baseImageRepo}@{sourceBaseImageSha}",
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{sourceRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "shared"
                                    }
                                }
                            }
                        }
                    });
            }

            if (sourceRuntimeDepsImageSha != null)
            {
                sourceRepos.Add(
                    new RepoData
                    {
                        Repo = runtimeRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDigest,
                                        BaseImageDigest = $"{runtimeDepsRepoQualified}@{sourceRuntimeDepsImageSha}",
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{sourceRuntimeCommitSha}/{runtimeDockerfileRelativePath}",
                                    }
                                }
                            }
                        }
                    });
            }

            ImageArtifactDetails sourceImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                                CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag })
                        },
                        new Dictionary<string, Tag>
                        {
                                { "shared", new Tag() }
                        },
                        ProductVersion)),
                CreateRepo(runtimeRepo,
                    CreateImage(
                        new Platform[]
                        {
                                CreatePlatform(runtimeDockerfileRelativePath, new string[] { tag })
                        },
                        productVersion: ProductVersion))
            );
            manifest.Registry = registry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsDigest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                        IsUnchanged = isRuntimeDepsCached,
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "shared"
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = runtimeRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDigest,
                                        BaseImageDigest = runtimeDepsDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeCommitSha}/{runtimeDockerfileRelativePath}",
                                        IsUnchanged = isRuntimeCached,
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            Times expectedTimes = isRuntimeDepsCached ? Times.Once() : Times.Never();
            dockerServiceMock.Verify(o => o.PullImage(runtimeDepsDigest, null, false), expectedTimes);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{overridePrefix}{runtimeDepsRepo}:{tag}", false), expectedTimes);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{overridePrefix}{runtimeDepsRepo}:shared", false), expectedTimes);

            expectedTimes = isRuntimeCached ? Times.Once() : Times.Never();
            dockerServiceMock.Verify(o => o.PullImage(runtimeDigest, null, false), expectedTimes);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDigest, $"{overridePrefix}{runtimeRepo}:{tag}", false), expectedTimes);
        }

        /// <summary>
        /// Tests a caching scenario where two platforms are defined that share the same Dockerfile. Only one of the platforms
        /// has an entry defined in the source image info file. It's configured so the first platform should be a cache hit and
        /// and the second platform should also be a cache hit even though it doesn't have an entry in the source image info file.
        /// </summary>
        [Fact]
        public async Task BuildCommand_Caching_SharedDockerfile_MissingSourceImageInfoEntry()
        {
            string runtimeDepsRepo = $"runtime-deps";
            string runtimeDeps2Repo = $"runtime-deps2";
            string runtimeDepsLinuxDigest = $"{runtimeDepsRepo}@sha1-linux";
            string runtimeDepsWindowsDigest = $"{runtimeDepsRepo}@sha1-windows";
            string runtimeDeps2Digest = $"{runtimeDeps2Repo}@sha1-linux";
            const string linuxTag = "linux-tag";
            const string windowsTag = "windows-tag";
            const string linuxBaseImageRepo = "linux-baserepo";
            const string windowsBaseImageRepo = "windows-baserepo";
            string linuxBaseImageTag = $"{linuxBaseImageRepo}:basetag";
            string windowsBaseImageTag = $"{windowsBaseImageRepo}:basetag";
            string runtimeDepsLinuxBaseImageDigest = $"{linuxBaseImageRepo}@sha";
            string runtimeDepsWindowsBaseImageDigest = $"{windowsBaseImageRepo}@sha";
            const string currentRuntimeDepsCommitSha = "commit-sha";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{linuxTag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsLinuxDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{windowsTag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsWindowsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDeps2Repo}:{linuxTag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDeps2Digest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(linuxBaseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsLinuxBaseImageDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(windowsBaseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsWindowsBaseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{linuxTag}", false))
                .Returns(createdDate);

            dockerServiceMock
               .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{windowsTag}", false))
               .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDeps2Repo}:{linuxTag}", false))
                .Returns(createdDate);

            string runtimeDepsLinuxDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/linux", tempFolderContext, linuxBaseImageTag);

            string runtimeDepsWindowsDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/windows", tempFolderContext, windowsBaseImageTag);

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsLinuxDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsWindowsDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.1";

            List<RepoData> sourceRepos = new List<RepoData>
            {
                new RepoData
                {
                    Repo = runtimeDepsRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = runtimeDepsLinuxDigest,
                                    BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                    Created = createdDate.ToUniversalTime(),
                                    SimpleTags =
                                    {
                                        linuxTag
                                    },
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}"
                                },
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsWindowsDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Windows",
                                    OsVersion = "nanoserver-2004",
                                    Digest = runtimeDepsWindowsDigest,
                                    BaseImageDigest = runtimeDepsWindowsBaseImageDigest,
                                    Created = createdDate.ToUniversalTime(),
                                    SimpleTags =
                                    {
                                        windowsTag
                                    },
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsWindowsDockerfileRelativePath}"
                                }
                            },
                            Manifest = new ManifestData
                            {
                                SharedTags = new List<string>
                                {
                                    "shared"
                                }
                            }
                        }
                    }
                }
            };
          
            ImageArtifactDetails sourceImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsLinuxDockerfileRelativePath, new string[] { linuxTag }),
                            CreatePlatform(
                                runtimeDepsWindowsDockerfileRelativePath,
                                new string[] { windowsTag },
                                OS.Windows,
                                "nanoserver-2004")
                        },
                        new Dictionary<string, Tag>
                        {
                            { "shared", new Tag() }
                        },
                        productVersion: ProductVersion)),
                CreateRepo(runtimeDeps2Repo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsLinuxDockerfileRelativePath, new string[] { linuxTag })
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsLinuxDigest,
                                        BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            linuxTag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}",
                                        IsUnchanged = true
                                    },
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsWindowsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Windows",
                                        OsVersion = "nanoserver-2004",
                                        Digest = runtimeDepsWindowsDigest,
                                        BaseImageDigest = runtimeDepsWindowsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            windowsTag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsWindowsDockerfileRelativePath}",
                                        IsUnchanged = true
                                    },
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = runtimeDeps2Repo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDeps2Digest,
                                        BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            linuxTag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.GetImageDigestAsync(linuxBaseImageTag, It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageDigestAsync(windowsBaseImageTag, It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.PullImage(linuxBaseImageTag, "linux/amd64", false), Times.Once);
            dockerServiceMock.Verify(o => o.PullImage(windowsBaseImageTag, "windows/amd64", false), Times.Once);
            dockerServiceMock.Verify(o => o.PullImage(runtimeDepsLinuxDigest, null, false), Times.Once);
            dockerServiceMock.Verify(o => o.PullImage(runtimeDepsWindowsDigest, null, false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsLinuxDigest, $"{runtimeDepsRepo}:shared", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsLinuxDigest, $"{runtimeDepsRepo}:{linuxTag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsLinuxDigest, $"{runtimeDeps2Repo}:{linuxTag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsWindowsDigest, $"{runtimeDepsRepo}:{windowsTag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsWindowsDigest, $"{runtimeDepsRepo}:shared", false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{linuxTag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{windowsTag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDeps2Repo}:{linuxTag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{linuxTag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{windowsTag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDeps2Repo}:{linuxTag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:shared", false));
            dockerServiceMock.Verify(o => o.GetCreatedDate(It.IsAny<string>(), false));
            dockerServiceMock.Verify(o =>
                o.BuildImage(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IDictionary<string, string>>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never);

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Tests a caching scenario where multiple platforms are defined that share the same Dockerfile. One of the platforms
        /// uses build args and has an entry defined in the source image info file which triggers a cache hit. Another platform
        /// has the same build args but does not have an entry in the image info file. But because it shares the same Dockerfile,
        /// it too should be a cache hit. The last platform again has no entry in the image info file and shares the same Dockerfile
        /// but it uses different build args so it should be a cache miss.
        /// </summary>
        [Fact]
        public async Task BuildCommand_Caching_SharedDockerfile_MissingSourceImageInfoEntry_DiffBuildArgs()
        {
            const string runtimeDepsRepo = "runtime-deps";
            const string runtimeDeps2Repo = "runtime-deps2";
            const string runtimeDeps3Repo = "runtime-deps3";
            string runtimeDepsDigest = $"{runtimeDepsRepo}@sha1";
            string runtimeDeps2Digest = $"{runtimeDeps2Repo}@sha1";
            string runtimeDeps3Digest = $"{runtimeDeps3Repo}@sha2";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string runtimeDepsBaseImageDigest = $"{baseImageRepo}@sha-base";
            const string currentRuntimeDepsCommitSha = "commit-sha";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDeps2Digest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDeps3Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDeps3Digest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsBaseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDeps2Repo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDeps3Repo}:{tag}", false))
                .Returns(createdDate);

            string runtimeDepsDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/os", tempFolderContext, baseImageTag);

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.1";

            List<RepoData> sourceRepos = new List<RepoData>
            {
                new RepoData
                {
                    Repo = runtimeDepsRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = runtimeDepsDigest,
                                    BaseImageDigest = runtimeDepsBaseImageDigest,
                                    Created = createdDate.ToUniversalTime(),
                                    SimpleTags =
                                    {
                                        tag
                                    },
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}"
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails sourceImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Platform runtimeDeps1Platform = CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag });
            runtimeDeps1Platform.BuildArgs = new Dictionary<string, string>
            {
                { "ARG1", "val1" },
                { "ARG2", "val2" }
            };

            Platform runtimeDeps2Platform = CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag });
            runtimeDeps2Platform.BuildArgs = runtimeDeps1Platform.BuildArgs;

            Platform runtimeDeps3Platform = CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag });
            runtimeDeps3Platform.BuildArgs = new Dictionary<string, string>
            {
                { "ARG1", "val1" },
                { "ARG2", "val2a" }
            };

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                            runtimeDeps1Platform
                        },
                        productVersion: ProductVersion)),
                CreateRepo(runtimeDeps2Repo,
                    CreateImage(
                        new Platform[]
                        {
                            runtimeDeps2Platform
                        },
                        productVersion: ProductVersion)),
                CreateRepo(runtimeDeps3Repo,
                    CreateImage(
                        new Platform[]
                        {
                            runtimeDeps3Platform
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsDigest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                        IsUnchanged = true
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = runtimeDeps2Repo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDeps2Digest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}"
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = runtimeDeps3Repo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDeps3Digest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.PullImage(runtimeDepsDigest, null, false));
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDepsRepo}:{tag}", false));
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDeps2Repo}:{tag}", false));
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDeps3Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDeps2Repo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDeps3Repo}:{tag}", false));
            dockerServiceMock.Verify(o =>
                o.BuildImage(
                    PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()));

            dockerServiceMock.Verify(o => o.PullImage(baseImageTag, "linux/amd64", false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync(It.IsAny<string>(), It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetCreatedDate(It.IsAny<string>(), false));
            dockerServiceMock.Verify(o => o.GetImageArch(baseImageTag, false));

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Tests a caching scenario where two platforms are defined that share the same Dockerfile and neither of them
        /// have a entry in the source image info file.
        /// </summary>
        [Fact]
        public async Task BuildCommand_Caching_SharedDockerfile_NoExistingImageInfoEntries()
        {
            const string runtimeDepsRepo = "runtime-deps";
            const string runtimeDeps2Repo = "runtime-deps2";
            string runtimeDepsDigest = $"{runtimeDepsRepo}@sha1";
            string runtimeDeps2Digest = $"{runtimeDeps2Repo}@sha1";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string runtimeDepsBaseImageDigest = $"{baseImageRepo}@sha";
            const string currentRuntimeDepsCommitSha = "commit-sha";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsBaseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDeps2Repo}:{tag}", false))
                .Returns(createdDate);

            string runtimeDepsDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/os", tempFolderContext, baseImageTag);

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.1";

            List<RepoData> sourceRepos = new List<RepoData>();

            ImageArtifactDetails sourceImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                             CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag })
                        },
                        productVersion: ProductVersion)),
                CreateRepo(runtimeDeps2Repo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag })
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsDigest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = runtimeDeps2Repo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDeps2Digest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDeps2Repo}:{tag}", false));

            string[] expectedTags = new string[]
            {
                $"{runtimeDepsRepo}:{tag}",
                $"{runtimeDeps2Repo}:{tag}"
            };

            foreach (string expectedTag in expectedTags)
            {
                dockerServiceMock.Verify(o => o.PushImage(expectedTag, false));

                dockerServiceMock.Verify(o =>
                    o.BuildImage(
                        PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        new string[] { expectedTag },
                        It.IsAny<IDictionary<string, string>>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Once);
            }

            dockerServiceMock.Verify(o => o.PullImage(baseImageTag, "linux/amd64", false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetCreatedDate(It.IsAny<string>(), false));
            dockerServiceMock.Verify(o => o.GetImageArch(baseImageTag, false));

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Tests a scenario where two platforms are defined that share the same Dockerfile.
        /// This scenario has caching allowed with a source image info but a build should occur for both platforms
        /// due to a base image digest diff.
        /// </summary>
        [Fact]
        public async Task BuildCommand_SharedDockerfile()
        {
            const string runtimeDepsRepo = "runtime-deps";
            const string runtimeDeps2Repo = "runtime-deps2";
            string runtimeDepsDigest = $"{runtimeDepsRepo}@sha1";
            string runtimeDeps2Digest = $"{runtimeDeps2Repo}@sha1";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string runtimeDepsBaseImageDigest = $"{baseImageRepo}@sha";
            const string currentRuntimeDepsCommitSha = "commit-sha";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsBaseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDeps2Repo}:{tag}", false))
                .Returns(createdDate);

            string runtimeDepsDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/os", tempFolderContext, baseImageTag);

            Mock<IGitService> gitServiceMock = new();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);

            BuildCommand command = new(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.1";

            List<RepoData> sourceRepos = new()
            {
                new RepoData
                {
                    Repo = runtimeDepsRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = runtimeDepsDigest,
                                    BaseImageDigest = runtimeDepsBaseImageDigest + "-diff",
                                    Created = createdDate.ToUniversalTime(),
                                    SimpleTags =
                                    {
                                        tag
                                    },
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}"
                                }
                            },
                            Manifest = new ManifestData
                            {
                                SharedTags = new List<string>
                                {
                                    "shared"
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails sourceImageArtifactDetails = new()
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                             CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag })
                        },
                        productVersion: ProductVersion)),
                CreateRepo(runtimeDeps2Repo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { tag })
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new()
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsDigest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = runtimeDeps2Repo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDeps2Digest,
                                        BaseImageDigest = runtimeDepsBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDeps2Repo}:{tag}", false));

            string[] expectedTags = new string[]
            {
                $"{runtimeDepsRepo}:{tag}",
                $"{runtimeDeps2Repo}:{tag}"
            };

            foreach (string expectedTag in expectedTags)
            {
                dockerServiceMock.Verify(o => o.PushImage(expectedTag, false));

                dockerServiceMock.Verify(o =>
                    o.BuildImage(
                        PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsDockerfileRelativePath)),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        new string[] { expectedTag },
                        It.IsAny<IDictionary<string, string>>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Once);
            }

            dockerServiceMock.Verify(o => o.PullImage(baseImageTag, "linux/amd64", false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetCreatedDate(It.IsAny<string>(), false));
            dockerServiceMock.Verify(o => o.GetImageArch(baseImageTag, false));

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Tests a caching scenario where a platform's digest has not changed since it was last published but a new
        /// tag has been introduced that isn't set in the source image info. The platform should not be marked as cached
        /// in that case.
        /// </summary>
        [Fact]
        public async Task BuildCommand_Caching_TagUpdate()
        {
            const string runtimeDepsRepo = "runtime-deps";
            string runtimeDepsDigest = $"{runtimeDepsRepo}@sha1";
            const string tag = "tag";
            const string newTag = "new-tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string runtimeDepsLinuxBaseImageDigest = $"{baseImageRepo}@sha";
            const string currentRuntimeDepsCommitSha = "commit-sha";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsDigest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsLinuxBaseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{newTag}", false))
                .Returns(createdDate);

            string runtimeDepsLinuxDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/linux", tempFolderContext, baseImageTag);

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsLinuxDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.0";

            List<RepoData> sourceRepos = new List<RepoData>
            {
                new RepoData
                {
                    Repo = runtimeDepsRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = runtimeDepsDigest,
                                    BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                    Created = createdDate.ToUniversalTime(),
                                    SimpleTags =
                                    {
                                        tag
                                    },
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}"
                                }
                            },
                            Manifest = new ManifestData
                            {
                                SharedTags = new List<string>
                                {
                                    "shared"
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails sourceImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsLinuxDockerfileRelativePath, new string[] { tag, newTag })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "shared", new Tag() }
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsDigest,
                                        BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            newTag,
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}"
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.PullImage(baseImageTag, "linux/amd64", false), Times.Once);
            dockerServiceMock.Verify(o => o.PullImage(runtimeDepsDigest, null, false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDepsRepo}:{tag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDepsRepo}:{newTag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsDigest, $"{runtimeDepsRepo}:shared", false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{newTag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{newTag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:shared", false));
            dockerServiceMock.Verify(o => o.GetCreatedDate(It.IsAny<string>(), false));
            dockerServiceMock.Verify(o =>
                o.BuildImage(
                    PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsLinuxDockerfileRelativePath)),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never);

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Tests a caching scenario where two platforms are defined that share the same Dockerfile. One of the platforms introduces
        /// a new tag that didn't exist in the source image info file. It should not be marked as cached.
        /// </summary>
        [Fact]
        public async Task BuildCommand_Caching_SharedDockerfile_TagUpdate()
        {
            const string runtimeDepsRepo = "runtime-deps";
            const string runtimeDeps2Repo = "runtime-deps2";
            string runtimeDepsLinuxDigest = $"{runtimeDepsRepo}@sha1";
            string runtimeDeps2Digest = $"{runtimeDeps2Repo}@sha1";
            const string tag = "tag";
            const string newTag = "new-tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string runtimeDepsLinuxBaseImageDigest = $"{baseImageRepo}@sha";
            const string currentRuntimeDepsCommitSha = "commit-sha";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsLinuxDigest);
            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDeps2Digest);

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(runtimeDepsLinuxBaseImageDigest);

            DateTime createdDate = DateTime.Now;

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDepsRepo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDeps2Repo}:{tag}", false))
                .Returns(createdDate);

            dockerServiceMock
                .Setup(o => o.GetCreatedDate($"{runtimeDeps2Repo}:{newTag}", false))
                .Returns(createdDate);

            string runtimeDepsLinuxDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/linux", tempFolderContext, baseImageTag);

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeDepsLinuxDockerfileRelativePath)), It.IsAny<bool>()))
                .Returns(currentRuntimeDepsCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "dest-image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

            const string ProductVersion = "1.0.0";

            List<RepoData> sourceRepos = new List<RepoData>
            {
                new RepoData
                {
                    Repo = runtimeDepsRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = runtimeDepsLinuxDigest,
                                    BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                    Created = createdDate.ToUniversalTime(),
                                    SimpleTags =
                                    {
                                        tag
                                    },
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}"
                                }
                            },
                            Manifest = new ManifestData
                            {
                                SharedTags = new List<string>
                                {
                                    "shared"
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails sourceImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(runtimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsLinuxDockerfileRelativePath, new string[] { tag })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "shared", new Tag() }
                        },
                        productVersion: ProductVersion)),
                CreateRepo(runtimeDeps2Repo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsLinuxDockerfileRelativePath, new string[] { tag, newTag })
                        },
                        productVersion: ProductVersion))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = runtimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDepsLinuxDigest,
                                        BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}",
                                        IsUnchanged = true
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = runtimeDeps2Repo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsLinuxDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = runtimeDeps2Digest,
                                        BaseImageDigest = runtimeDepsLinuxBaseImageDigest,
                                        Created = createdDate.ToUniversalTime(),
                                        SimpleTags =
                                        {
                                            newTag,
                                            tag
                                        },
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{currentRuntimeDepsCommitSha}/{runtimeDepsLinuxDockerfileRelativePath}",
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.PullImage(runtimeDepsLinuxDigest, null, false), Times.Once);
            dockerServiceMock.Verify(o => o.PullImage(baseImageTag, "linux/amd64", false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageDigestAsync(baseImageTag, It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsLinuxDigest, $"{runtimeDepsRepo}:{tag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsLinuxDigest, $"{runtimeDepsRepo}:shared", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsLinuxDigest, $"{runtimeDeps2Repo}:{tag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.CreateTag(runtimeDepsLinuxDigest, $"{runtimeDeps2Repo}:{newTag}", false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDepsRepo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDeps2Repo}:{tag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{runtimeDeps2Repo}:{newTag}", It.IsAny<IRegistryCredentialsHost>(), false), Times.Once);
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDeps2Repo}:{tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDeps2Repo}:{newTag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{runtimeDepsRepo}:shared", false));
            dockerServiceMock.Verify(o =>
                o.BuildImage(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IDictionary<string, string>>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never);
            dockerServiceMock.Verify(o => o.GetCreatedDate(It.IsAny<string>(), false));

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies the command pulls base images from a mirror location.
        /// </summary>
        [Theory]
        [InlineData(false, "library/baserepo", "baserepo")]
        [InlineData(false, "library/baserepo", "library/baserepo")]
        [InlineData(true, "library/baserepo", "baserepo")]
        [InlineData(true, "library/baserepo", "library/baserepo")]
        public async Task BuildCommand_MirroredImages(bool hasCachedImage, string srcBaseImageRepo, string referencedBaseImageRepo)
        {
            const string Registry = "mcr.microsoft.com";
            const string RegistryOverride = "dotnetdocker.azurecr.io";
            const string RuntimeDepsRepo = "runtime-deps";
            const string RuntimeRepo = "runtime";
            const string AspnetRepo = "aspnet";
            const string RuntimeDepsDigest = "sha256:c74364a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd638c";
            const string RuntimeDigest = "sha256:adc914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed99227";
            const string AspnetDigest = "sha256:781914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed0045a";
            const string Tag = "tag";
            const string RepoPrefix = "prefix/";
            string srcBaseImageTag = $"{srcBaseImageRepo}:basetag";
            string referencedBaseImageTag = $"{referencedBaseImageRepo}:basetag";

            const string SourceRepoPrefix = "my-mirror/";
            string mirrorBaseTag = $"{RegistryOverride}/{SourceRepoPrefix}{srcBaseImageTag}";
            string srcBaseImageDigest =
                $"{srcBaseImageRepo}@sha256:d21234a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd1349";
            string expectedImageInfoBaseImageDigest =
                $"{referencedBaseImageRepo}@sha256:d21234a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd1349";
            string mirrorBaseImageDigest =
                $"{RegistryOverride}/{SourceRepoPrefix}{srcBaseImageDigest}";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            if (hasCachedImage)
            {
                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{Registry}/{RuntimeDepsRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync($"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}");

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{Registry}/{RuntimeRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync($"{Registry}/{RuntimeRepo}@{RuntimeDigest}");

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{Registry}/{AspnetRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync($"{Registry}/{AspnetRepo}@{AspnetDigest}");

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync($"{RegistryOverride}/{RuntimeDepsRepo}@{RuntimeDepsDigest}");

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync($"{RegistryOverride}/{RuntimeRepo}@{RuntimeDigest}");

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{AspnetRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync($"{RegistryOverride}/{AspnetRepo}@{AspnetDigest}");
            }
            else
            {
                // Locally built images will not have a digest until they get pushed. So don't return a digest until
                // the appropriate request.
                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(callCount => callCount > 2 ? $"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}@{RuntimeDepsDigest}" : null);

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(callCount => callCount > 1 ? $"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}@{RuntimeDigest}" : null);

                dockerServiceMock
                    .Setup(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{AspnetRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                    .ReturnsAsync(callCount => callCount > 0 ? $"{RegistryOverride}/{RepoPrefix}{AspnetRepo}@{AspnetDigest}" : null);
            }
            
            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync(mirrorBaseTag, It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync(mirrorBaseImageDigest);

            DateTime createdDate = DateTime.Now.ToUniversalTime();
            dockerServiceMock
                .Setup(o => o.GetCreatedDate(It.IsAny<string>(), false))
                .Returns(createdDate);

            string runtimeDepsDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime-deps/os", tempFolderContext, referencedBaseImageTag);

            string runtimeDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/runtime/os", tempFolderContext, $"$REPO:{Tag}");

            string aspnetDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/aspnet/os", tempFolderContext, $"$REPO:{Tag}");

            const string dockerfileCommitSha = "mycommit";
            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(dockerfileCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.ImageInfoSourcePath = Path.Combine(tempFolderContext.Path, "src-image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";
            command.Options.SourceRepoPrefix = SourceRepoPrefix;
            command.Options.RegistryOverride = RegistryOverride;
            command.Options.RepoPrefix = RepoPrefix;

            const string ProductVersion = "1.0.1";

            List<RepoData> sourceRepos = new List<RepoData>
            {
                new RepoData
                {
                    Repo = RuntimeDepsRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDepsDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = $"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}",
                                    BaseImageDigest = hasCachedImage ? mirrorBaseImageDigest : mirrorBaseImageDigest + "b",
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                    SimpleTags = new List<string>
                                    {
                                        Tag
                                    }
                                }
                            }
                        }
                    }
                },
                new RepoData
                {
                    Repo = RuntimeRepo,
                    Images =
                    {
                        new ImageData
                        {
                            ProductVersion = ProductVersion,
                            Platforms =
                            {
                                new PlatformData
                                {
                                    Dockerfile = runtimeDockerfileRelativePath,
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "focal",
                                    Digest = $"{Registry}/{RuntimeRepo}@{RuntimeDigest}",
                                    BaseImageDigest = hasCachedImage ?
                                        $"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}" :
                                        $"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}" + "b",
                                    CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDockerfileRelativePath}",
                                    SimpleTags = new List<string>
                                    {
                                        Tag
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails sourceImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = sourceRepos.ToList()
            };

            string sourceImageArtifactDetailsOutput = JsonHelper.SerializeObject(sourceImageArtifactDetails);
            File.WriteAllText(command.Options.ImageInfoSourcePath, sourceImageArtifactDetailsOutput);

            Manifest manifest = CreateManifest(
                CreateRepo(RuntimeDepsRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(runtimeDepsDockerfileRelativePath, new string[] { Tag })
                        },
                        productVersion: ProductVersion)),
                CreateRepo(RuntimeRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatformWithRepoBuildArg(
                                runtimeDockerfileRelativePath,
                                $"$(Repo:{RuntimeDepsRepo})",
                                new string[] { Tag })
                        },
                        productVersion: ProductVersion)),
                CreateRepo(AspnetRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatformWithRepoBuildArg(
                                aspnetDockerfileRelativePath,
                                $"$(Repo:{RuntimeRepo})",
                                new string[] { Tag })
                        },
                        productVersion: ProductVersion))
            );
            manifest.Registry = Registry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            ImageArtifactDetails expectedOutputImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = RuntimeDepsRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDepsDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = $"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}",
                                        BaseImageDigest = expectedImageInfoBaseImageDigest,
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDepsDockerfileRelativePath}",
                                        Created = createdDate,
                                        IsUnchanged = hasCachedImage,
                                        SimpleTags = new List<string>
                                        {
                                            Tag
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = RuntimeRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = runtimeDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = $"{Registry}/{RuntimeRepo}@{RuntimeDigest}",
                                        BaseImageDigest = $"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}",
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{runtimeDockerfileRelativePath}",
                                        Created = createdDate,
                                        IsUnchanged = hasCachedImage,
                                        SimpleTags = new List<string>
                                        {
                                            Tag
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = AspnetRepo,
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = ProductVersion,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = aspnetDockerfileRelativePath,
                                        Architecture = "amd64",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Digest = $"{Registry}/{AspnetRepo}@{AspnetDigest}",
                                        BaseImageDigest = $"{Registry}/{RuntimeRepo}@{RuntimeDigest}",
                                        CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{aspnetDockerfileRelativePath}",
                                        Created = createdDate,
                                        SimpleTags = new List<string>
                                        {
                                            Tag
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedOutputImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

            Assert.Equal(expectedOutput, actualOutput);

            dockerServiceMock.Verify(o => o.PullImage(mirrorBaseTag, "linux/amd64", false));
            dockerServiceMock.Verify(o => o.CreateTag(mirrorBaseTag, referencedBaseImageTag, false));

            dockerServiceMock.Verify(
                o => o.BuildImage(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()));

            if (hasCachedImage)
            {
                dockerServiceMock.Verify(o => o.PullImage($"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}", null, false));
                dockerServiceMock.Verify(o => o.CreateTag($"{Registry}/{RuntimeDepsRepo}@{RuntimeDepsDigest}", $"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", false));

                dockerServiceMock.Verify(o => o.PullImage($"{Registry}/{RuntimeRepo}@{RuntimeDigest}", null, false));
                dockerServiceMock.Verify(o => o.CreateTag($"{Registry}/{RuntimeRepo}@{RuntimeDigest}", $"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", false));
            }
            else
            {
                dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));
                dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            }

            dockerServiceMock.Verify(o => o.GetImageDigestAsync(mirrorBaseTag, It.IsAny<IRegistryCredentialsHost>(), false));
            
            dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{RegistryOverride}/{RepoPrefix}{AspnetRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));

            dockerServiceMock.Verify(o => o.PushImage($"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", false));
            dockerServiceMock.Verify(o => o.PushImage($"{RegistryOverride}/{RepoPrefix}{AspnetRepo}:{Tag}", false));

            dockerServiceMock.Verify(o => o.GetCreatedDate($"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", false));
            dockerServiceMock.Verify(o => o.GetCreatedDate($"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", false));
            dockerServiceMock.Verify(o => o.GetCreatedDate($"{RegistryOverride}/{RepoPrefix}{AspnetRepo}:{Tag}", false));

            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{RegistryOverride}/{RepoPrefix}{AspnetRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));

            if (!hasCachedImage)
            {
                dockerServiceMock.Verify(o => o.GetImageArch(mirrorBaseTag, false));
                dockerServiceMock.Verify(o => o.GetImageArch($"{RegistryOverride}/{RepoPrefix}{RuntimeDepsRepo}:{Tag}", false));
            }
            
            dockerServiceMock.Verify(o => o.GetImageArch($"{RegistryOverride}/{RepoPrefix}{RuntimeRepo}:{Tag}", false));

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Tests the logic of image mirroring when the base image is considered to be external from a graph perspective.
        /// </summary>
        [Theory]
        [InlineData("mcr.microsoft.com", "mcr.microsoft.com")]
        [InlineData("other-registry.com", "mcr.microsoft.com")]
        public async Task BuildCommand_MirroredImages_External(string baseImageRegistry, string manifestRegistry)
        {
            const string RegistryOverride = "dotnetdocker.azurecr.io";
            const string RuntimeRepo = "runtime";
            const string SamplesRepo = "samples";
            const string RuntimeDigest = "sha256:adc914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed99227";
            const string SampleDigest = "sha256:781914a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73ed0045a";
            const string Tag = "tag";
            const string SourceRepoPrefix = "mirror/";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            bool isExternallyOwnedBaseImage = baseImageRegistry != manifestRegistry;

            string baseImageRepoPrefix = $"{baseImageRegistry}";
            if (isExternallyOwnedBaseImage)
            {
                baseImageRepoPrefix = $"{RegistryOverride}/{SourceRepoPrefix}{baseImageRegistry}";
            }

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{baseImageRepoPrefix}/{RuntimeRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync($"{baseImageRepoPrefix}/{RuntimeRepo}@{RuntimeDigest}");

            dockerServiceMock
                .Setup(o => o.GetImageDigestAsync($"{RegistryOverride}/{SamplesRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false))
                .ReturnsAsync($"{RegistryOverride}/{SamplesRepo}@{SampleDigest}");

            string sampleDockerfileRelativePath = DockerfileHelper.CreateDockerfile(
                "1.0/samples/os", tempFolderContext, $"{baseImageRegistry}/{RuntimeRepo}:{Tag}");

            const string dockerfileCommitSha = "mycommit";
            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.GetCommitSha(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(dockerfileCommitSha);

            BuildCommand command = new BuildCommand(
                dockerServiceMock.Object,
                Mock.Of<ILoggerService>(),
                gitServiceMock.Object,
                Mock.Of<IProcessService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.IsPushEnabled = true;
            command.Options.SourceRepoUrl = "https://github.com/dotnet/test";
            command.Options.RegistryOverride = RegistryOverride;
            command.Options.SourceRepoPrefix = SourceRepoPrefix;

            const string ProductVersion = "1.0.1";

            Manifest manifest = CreateManifest(
                CreateRepo(SamplesRepo,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                sampleDockerfileRelativePath,
                                new string[] { Tag })
                        },
                        productVersion: ProductVersion))
            );
            manifest.Registry = manifestRegistry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(
                o => o.BuildImage(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()));

            dockerServiceMock.Verify(o => o.PullImage($"{baseImageRepoPrefix}/{RuntimeRepo}:{Tag}", "linux/amd64", false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{baseImageRepoPrefix}/{RuntimeRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.GetImageDigestAsync($"{RegistryOverride}/{SamplesRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));
            dockerServiceMock.Verify(o => o.PushImage($"{RegistryOverride}/{SamplesRepo}:{Tag}", false));
            dockerServiceMock.Verify(o => o.GetCreatedDate($"{RegistryOverride}/{SamplesRepo}:{Tag}", false));
            dockerServiceMock.Verify(o => o.GetImageManifestLayersAsync($"{RegistryOverride}/{SamplesRepo}:{Tag}", It.IsAny<IRegistryCredentialsHost>(), false));

            if (isExternallyOwnedBaseImage)
            {
                dockerServiceMock.Verify(o =>
                    o.CreateTag($"{baseImageRepoPrefix}/{RuntimeRepo}:{Tag}", $"{baseImageRegistry}/{RuntimeRepo}:{Tag}", false));
            }

            dockerServiceMock.Verify(o => o.GetImageArch($"{baseImageRepoPrefix}/{RuntimeRepo}:{Tag}", false));

            dockerServiceMock.VerifyNoOtherCalls();
        }

        private static Mock<IDockerService> CreateDockerServiceMock(string buildOutput = null)
        {
            Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
            dockerServiceMock
                .SetupGet(o => o.Architecture)
                .Returns(Architecture.AMD64);

            dockerServiceMock
                .Setup(o =>
                    o.BuildImage(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<IEnumerable<string>>(),
                        It.IsAny<IDictionary<string, string>>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()))
                .Returns(buildOutput ?? string.Empty);

            dockerServiceMock
                .Setup(o => o.GetImageArch(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((Architecture.AMD64, null));

            return dockerServiceMock;
        }
    }
}
