// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Moq;
using Newtonsoft.Json;
using Xunit;

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PublishMcrDocsCommandTests
    {
        private const string ProductFamilyReadmePath = "ProductFamilyReadme.md";
        private const string RepoReadmePath = "RepoReadme.md";
        private const string TagsYamlPath = "tags.yml";
        private const string DefaultReadme = "Default Readme Contents\n# Full Tag Listing\n<!--End of generated tags-->\n";
        private const string ReadmeTemplatePath = "Readme.Template.md";
        private const string AboutRepoTemplatePath = "About.repo.Template.md";
        private const string AboutRepoTemplate =
@"Referenced Template Content";
        private const string ReadmeTemplate =
@"About {{if IS_PRODUCT_FAMILY:Product Family^else:{{SHORT_REPO}}}}
{{if !IS_PRODUCT_FAMILY:{{InsertTemplate(join(filter([""About"", SHORT_REPO, ""Template"", ""md""], len), "".""))}}}}";

        [Fact]
        public async Task ExcludeProductFamilyReadme()
        {
            Mock<IGitHubClient> gitHubClientMock = new();
            gitHubClientMock
                .Setup(o => o.GetReferenceAsync(It.IsAny<GitHubProject>(), It.IsAny<string>()))
                .ReturnsAsync(new GitReference
                {
                    Object = new GitReferenceObject()
                });

            gitHubClientMock
                .Setup(o => o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<GitObject[]>()))
                .ReturnsAsync(new GitTree());

            gitHubClientMock
                .Setup(o => o.PostCommitAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .ReturnsAsync(new GitCommit());

            Mock<IGitHubClientFactory> gitHubClientFactoryMock = new();
            gitHubClientFactoryMock
                .Setup(o => o.GetClient(It.IsAny<GitHubAuth>(), false))
                .Returns(gitHubClientMock.Object);

            PublishMcrDocsCommand command = new(Mock.Of<IGitService>(), gitHubClientFactoryMock.Object, Mock.Of<ILoggerService>());

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            DockerfileHelper.CreateFile(ProductFamilyReadmePath, tempFolderContext, DefaultReadme);
            DockerfileHelper.CreateFile(RepoReadmePath, tempFolderContext, DefaultReadme);
            DockerfileHelper.CreateFile(AboutRepoTemplatePath, tempFolderContext, AboutRepoTemplate);
            DockerfileHelper.CreateFile(ReadmeTemplatePath, tempFolderContext, ReadmeTemplate);

            // Create MCR tags metadata template file
            StringBuilder tagsMetadataTemplateBuilder = new();
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlRepo:repo)");
            tagsMetadataTemplateBuilder.Append($"$(McrTagsYmlTagGroup:tag)");
            string tagsMetadataTemplatePath = Path.Combine(tempFolderContext.Path, TagsYamlPath);
            File.WriteAllText(tagsMetadataTemplatePath, tagsMetadataTemplateBuilder.ToString());

            Repo repo;
            Manifest manifest = CreateManifest(
                repo = CreateRepo("dotnet/repo", new Image[]
                {
                    CreateImage(
                        CreatePlatform(CreateDockerfile("1.0/runtime/linux", tempFolderContext), new string[] { "tag" }))
                }, RepoReadmePath, ReadmeTemplatePath, Path.GetFileName(tagsMetadataTemplatePath)));
            manifest.Registry = "mcr.microsoft.com";
            manifest.Readme = ProductFamilyReadmePath;
            manifest.ReadmeTemplate = ReadmeTemplatePath;
            repo.Id = "repo";

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            command.Options.Manifest = manifestPath;
            command.Options.ExcludeProductFamilyReadme = true;
            command.LoadManifest();

            await command.ExecuteAsync();

            // Verify published file list does not contain ProductFamilyReadmePath
            gitHubClientMock
                .Verify(o =>
                    o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(),
                        It.Is<GitObject[]>(objs =>
                            objs.Length == 2 &&
                            Path.GetFileName(objs[0].Path) == RepoReadmePath &&
                            Path.GetFileName(objs[1].Path) == TagsYamlPath)));
        }
    }
}
