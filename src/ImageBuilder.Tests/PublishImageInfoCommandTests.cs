#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class PublishImageInfoCommandTests : IDisposable
{
    #nullable enable annotations
    public TestContext? TestContext { get; set; }

    #nullable disable annotations

    private readonly List<string> _foldersToDelete = [];

    public void Dispose()
    {
        _foldersToDelete
            .Where(folder => Directory.Exists(folder))
            .ForEach(folder => Directory.Delete(folder, recursive: true));
    }

    /// <summary>
    /// Verifies the command will publish the specified image info to the repo.
    /// </summary>
    [TestMethod]
    public async Task PublishImageInfoCommand_HappyPath()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
        string repo2Image2DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);
        Manifest manifest = CreateManifest(
            CreateRepo("repo1",
                CreateImage(
                    [
                            CreatePlatform(repo1Image1DockerfilePath, ["tag1"])
                    ],
                    productVersion: "1.0")),
            CreateRepo("repo2",
                CreateImage(
                    [
                            CreatePlatform(repo2Image2DockerfilePath, ["tag1"])
                    ],
                    productVersion: "2.0"))
        );

        RepoData repo2;

        ImageArtifactDetails srcImageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "newtag"
                                        ])
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "newshared1",
                                        "newshared2"
                                    ]
                                }
                            }
                        }
                    },
                    {
                        repo2 = new RepoData
                        {
                            Repo = "repo2",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo2Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag1"
                                        ])
                                    },
                                    ProductVersion = "2.0"
                                }
                            }
                        }
                    }
                }
        };

        string file = Path.Combine(tempFolderContext.Path, "image-info.json");
        File.WriteAllText(file, JsonHelper.SerializeObject(srcImageArtifactDetails));

        GitOptions gitOptions = new GitOptions
        {
            GitHubAuthOptions = new GitHubAuthOptions(AuthToken: "token"),
            Repo = "PublishImageInfoCommand_ReplaceContent",
            Branch = "testBranch",
            Path = "imageinfo.json",
            Email = "test@contoso.com",
            Username = "test"
        };

        AzdoOptions azdoOptions = new AzdoOptions
        {
            AccessToken = "azdo-token",
            AzdoBranch = "testBranch",
            AzdoRepo = "testRepo",
            Organization = "azdo-org",
            Project = "azdo-project",
            AzdoPath = "imageinfo.json"
        };

        Mock<IRepository> repositoryMock = GetRepositoryMock();
        Mock<IGitService> gitServiceMock = GetGitServiceMock(repositoryMock.Object, gitOptions.Path);

        string actualImageArtifactDetailsContents = null;
        gitServiceMock
            .Setup(o => o.Stage(It.IsAny<IRepository>(), It.IsAny<string>()))
            .Callback((IRepository repo, string path) => actualImageArtifactDetailsContents = File.ReadAllText(path));

        PublishImageInfoCommand command = new(
            TestHelper.CreateManifestJsonService(),
            gitServiceMock.Object,
            Mock.Of<IOctokitClientFactory>(),
            Mock.Of<ILogger<PublishImageInfoCommand>>(),
            TestHelper.CreateArtifactService(tempFolderContext.Path));
        command.Options.ImageInfoPath = file;
        command.Options.GitOptions = gitOptions;
        command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");

        File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

        command.LoadManifest();
        await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

        actualImageArtifactDetailsContents.Trim().ShouldBe(JsonHelper.SerializeObject(srcImageArtifactDetails));

        VerifyMocks(repositoryMock);
    }
    private static void VerifyMocks(Mock<IRepository> repositoryMock)
    {
        Mock<Network> networkMock = Mock.Get(repositoryMock.Object.Network);

        networkMock.Verify(o => o.Push(It.IsAny<Branch>(), It.IsAny<PushOptions>()));
    }

    private static Mock<IRepository> GetRepositoryMock()
    {
        List<Remote> remotes = [];

        Mock<RemoteCollection> remoteCollection = new Mock<RemoteCollection>();
        remoteCollection
            .Setup(o => o.GetEnumerator())
            .Returns(() => remotes.GetEnumerator());

        Mock<Network> networkMock = new Mock<Network>();
        networkMock
            .SetupGet(o => o.Remotes)
            .Returns(remoteCollection.Object);

        Mock<IRepository> repositoryMock = new Mock<IRepository>();
        repositoryMock
            .SetupGet(o => o.Network)
            .Returns(networkMock.Object);

        Mock<LibGit2Sharp.Index> indexMock = new Mock<LibGit2Sharp.Index>();

        repositoryMock
            .SetupGet(o => o.Index)
            .Returns(indexMock.Object);

        Mock<Branch> branchMock = new Mock<Branch>();

        Mock<BranchCollection> branchCollectionMock = new Mock<BranchCollection>();
        branchCollectionMock
            .Setup(o => o[It.IsAny<string>()])
            .Returns(branchMock.Object);

        repositoryMock
            .SetupGet(o => o.Branches)
            .Returns(branchCollectionMock.Object);

        Mock<Commit> commitMock = new Mock<Commit>();

        repositoryMock
            .Setup(o => o.Commit(It.IsAny<string>(), It.IsAny<Signature>(), It.IsAny<Signature>(), It.IsAny<CommitOptions>()))
            .Returns(commitMock.Object);

        return repositoryMock;
    }

    private Mock<IGitService> GetGitServiceMock(IRepository repository, string imageInfoPath)
    {
        Mock<IGitService> gitServiceMock = new Mock<IGitService>();
        gitServiceMock
            .Setup(o => o.CloneRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CloneOptions>()))
            .Callback((string sourceUrl, string workdirPath, CloneOptions cloneOptions) =>
            {
                Directory.CreateDirectory(workdirPath);
                File.WriteAllText(Path.Combine(workdirPath, imageInfoPath), string.Empty);
                _foldersToDelete.Add(workdirPath);
            })
            .Returns(repository);
        return gitServiceMock;
    }
}
