#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class WaitForMcrImageIngestionCommandTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("public/")]
        [InlineData("internal/private/")]
        public async Task SuccessfulPublish(string repoPrefix)
        {
            DateTime baselineTime = DateTime.Now;
            const string manifestDigest1 = "repo@sha256:manifestDigest1";
            const string sharedTag1 = "sharedTag1";
            const string sharedTag2 = "sharedTag2";
            const string platformTag1 = "platformTag1";
            const string platformTag2 = "platformTag2";
            const string platformTag3 = "platformTag3";
            const string repo1 = "repo1";
            const string repo2 = "repo2";
            const string platformDigest1 = "repo@sha256:platformDigest1";
            const string platformDigest2 = "repo@sha256:platformDigest2";

            Mock<IMarImageIngestionReporter> imageIngestionReporterMock = new();

            WaitForMcrImageIngestionCommand command = new(
                Mock.Of<ILoggerService>(),
                imageIngestionReporterMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);
            string dockerfile2Path = CreateDockerfile("1.0/repo2/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1, platformTag2 })
                        })),
                CreateRepo(repo2,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile2Path, new string[] { platformTag3 })
                        }))
            );

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        new RepoData
                        {
                            Repo = repo1,
                            Images =
                            {
                                new ImageData
                                {
                                    Manifest = new ManifestData
                                    {
                                        Digest = manifestDigest1,
                                        SharedTags = new List<string>
                                        {
                                            sharedTag1,
                                            sharedTag2
                                        }
                                    },
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1,
                                                platformTag2
                                            },
                                            digest: platformDigest1)
                                    }
                                }
                            }
                        }
                    },
                    {
                        new RepoData
                        {
                            Repo = repo2,
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfile2Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag3
                                            },
                                            digest: platformDigest2)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.RepoPrefix = repoPrefix;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await command.ExecuteAsync();

            List<DigestInfo> expectedDigestInfos =
                [
                    new(DockerHelper.GetDigestSha(manifestDigest1), repoPrefix + repo1, [ sharedTag1, sharedTag2 ]),
                    new(DockerHelper.GetDigestSha(platformDigest1), repoPrefix + repo1, [ platformTag1, platformTag2 ]),
                    new(DockerHelper.GetDigestSha(platformDigest2), repoPrefix + repo2, [ platformTag3 ]),
                ];

            imageIngestionReporterMock
                .Verify(o => o.ReportImageStatusesAsync(
                    It.IsAny<IServiceConnection>(),
                    It.Is<IEnumerable<DigestInfo>>(infos =>
                        infos.SequenceEqual(expectedDigestInfos, DigestInfoEqualityComparer.Instance)),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<DateTime>()));
        }

        [Theory]
        [InlineData("manifestDigest1")] // Same as the primary digest
        [InlineData("manifestDigest2")] // Different from the primary digest
        public async Task SyndicatedTags(string syndicatedManifestDigest)
        {
            DateTime baselineTime = DateTime.Now;
            const string registry = "mcr.microsoft.com";
            const string primaryManifestDigest = "manifestDigest1";
            string repo1ManifestDigest1 = $"{registry}/repo1@sha256:{primaryManifestDigest}";
            string repo2ManifestDigest1 = $"{registry}/repo2@sha256:{syndicatedManifestDigest}";
            const string sharedTag1 = "sharedTag1";
            const string platformTag1 = "platformTag1";
            const string repo1 = "repo1";
            string repo1PlatformDigest1 = $"{registry}repo1@sha256:platformDigest1";
            string repo2PlatformDigest1 = $"{registry}repo2@sha256:platformDigest1";

            Mock<IMarImageIngestionReporter> imageIngestionReporterMock = new();

            WaitForMcrImageIngestionCommand command = new(
                Mock.Of<ILoggerService>(),
                imageIngestionReporterMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);

            const string syndicatedRepo = "repo2";

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1 })
                        },
                        sharedTags: new Dictionary<string, Tag>
                        {
                            {
                                sharedTag1,
                                new Tag
                                {
                                    Syndication = new TagSyndication
                                    {
                                        Repo = syndicatedRepo,
                                        DestinationTags = new string[0]
                                    }
                                }
                            }
                        }))
            );
            manifest.Registry = registry;

            Platform platform = manifest.Repos.First().Images.First().Platforms.First();
            platform.Tags[platformTag1].Syndication = new TagSyndication
            {
                Repo = syndicatedRepo,
                DestinationTags = new string[]
                {
                    $"{platformTag1}a",
                    $"{platformTag1}b"
                }
            };

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        new RepoData
                        {
                            Repo = repo1,
                            Images =
                            {
                                new ImageData
                                {
                                    Manifest = new ManifestData
                                    {
                                        Digest = repo1ManifestDigest1,
                                        SyndicatedDigests = new List<string>
                                        {
                                            repo2ManifestDigest1
                                        },
                                        SharedTags = new List<string>
                                        {
                                            sharedTag1
                                        }
                                    },
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1
                                            },
                                            digest: repo1PlatformDigest1)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromMinutes(1);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await command.ExecuteAsync();

            List<DigestInfo> expectedDigestInfos =
                [
                    new(DockerHelper.GetDigestSha(repo1ManifestDigest1), repo1, [ sharedTag1 ]),
                    new(DockerHelper.GetDigestSha(repo2ManifestDigest1), syndicatedRepo, [ sharedTag1 ]),
                    new(DockerHelper.GetDigestSha(repo1PlatformDigest1), repo1, [ platformTag1 ]),
                    new(DockerHelper.GetDigestSha(repo1PlatformDigest1), syndicatedRepo, [ $"{platformTag1}a", $"{platformTag1}b" ]),
                ];

            imageIngestionReporterMock
                .Verify(o => o.ReportImageStatusesAsync(
                    It.IsAny<IServiceConnection>(),
                    It.Is<IEnumerable<DigestInfo>>(infos =>
                        infos.SequenceEqual(expectedDigestInfos, DigestInfoEqualityComparer.Instance)),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<DateTime>()));
        }
    }
}
