// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Newtonsoft.Json;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class McrTagsMetadataGeneratorTests
    {
        /// <summary>
        /// Verfies the Dockerfile path is set correctly 
        /// </summary>
        /// <remarks>
        /// If the source branch isn't set, the commit SHA of the Dockerfile will be used in the URL
        /// See https://github.com/dotnet/dotnet-docker/issues/1436
        /// </remarks>
        [Theory]
        [InlineData("branch")]
        [InlineData(null)]
        public void DockerfileUrl(string sourceRepoBranch)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            const string SourceRepoUrl = "https://www.github.com/dotnet/dotnet-docker";
            const string RepoName = "repo";
            const string TagName = "tag";

            // Create Dockerfile
            string DockerfileDir = $"1.0/{RepoName}/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, DockerfileDir));
            string dockerfileRelativePath = Path.Combine(DockerfileDir, "Dockerfile");
            string dockerfileFullPath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(dockerfileFullPath, "FROM base:tag");

            // Create MCR tags metadata template file
            StringBuilder tagsMetadataTemplateBuilder = new StringBuilder();
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlRepo:{RepoName})");
            tagsMetadataTemplateBuilder.Append($"$(McrTagsYmlTagGroup:{TagName})");
            string tagsMetadataTemplatePath = Path.Combine(tempFolderContext.Path, "tags.yaml");
            File.WriteAllText(tagsMetadataTemplatePath, tagsMetadataTemplateBuilder.ToString());

            // Create manifest
            Manifest manifest = ManifestHelper.CreateManifest(
                ManifestHelper.CreateRepo(RepoName,
                    new Image[]
                    {
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { TagName }))
                    },
                    mcrTagsMetadataTemplatePath: Path.GetFileName(tagsMetadataTemplatePath))
            );
            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            // Load manifest
            IManifestOptionsInfo manifestOptions = GetManifestOptions(manifestPath);
            ManifestInfo manifestInfo = ManifestInfo.Load(manifestOptions);
            RepoInfo repo = manifestInfo.AllRepos.First();

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            const string DockerfileSha = "random_sha";

            if (sourceRepoBranch == null)
            {
                gitServiceMock
                    .Setup(o => o.GetCommitSha(dockerfileFullPath, true))
                    .Returns(DockerfileSha);
            }

            // Execute generator
            string result = McrTagsMetadataGenerator.Execute(
                gitServiceMock.Object, manifestInfo, repo, SourceRepoUrl, sourceRepoBranch);

            Models.Mcr.McrTagsMetadata tagsMetadata = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<Models.Mcr.McrTagsMetadata>(result);

            string branchOrSha = sourceRepoBranch ?? DockerfileSha;
            Assert.Equal($"{SourceRepoUrl}/blob/{branchOrSha}/{DockerfileDir}/Dockerfile",
                tagsMetadata.Repos[0].TagGroups[0].Dockerfile);
        }

        private static IManifestOptionsInfo GetManifestOptions(string manifestPath)
        {
            Mock<IManifestOptionsInfo> manifestOptionsMock = new Mock<IManifestOptionsInfo>();

            manifestOptionsMock
                .SetupGet(o => o.Manifest)
                .Returns(manifestPath);

            manifestOptionsMock
                .Setup(o => o.GetManifestFilter())
                .Returns(new ManifestFilter());

            return manifestOptionsMock.Object;
        }
    }
}
