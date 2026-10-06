#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class ManifestInfoTests
    {
        private static string s_dockerfilePath = "testDockerfile";
        private static string s_repoJson =
$@"
  ""repos"": [
    {CreateRepo("testRepo", s_dockerfilePath)}
  ]
";

        [TestMethod]
        public void Load_Import_Variables()
        {
            string includeManifestPath = "manifest.variables.json";
            string variableOneName = "variable1";
            string variableOneValue = "value1";
            string variableTwoName = "variable2";
            string variableTwoValue = "value2";
            string manifest =
$@"
{{
  ""includes"": [
    ""{includeManifestPath}""
  ],
  ""variables"": {{
      ""{variableOneName}"": ""{variableOneValue}""
  }},
{s_repoJson}
}}";
            string includeManifest =
$@"
{{
  ""variables"": {{
      ""{variableTwoName}"": ""{variableTwoValue}""
  }}
}}";

            ManifestInfo manifestInfo = LoadManifestInfo(manifest, includeManifestPath, includeManifest);
            manifestInfo.Model.Variables.Count.ShouldBe(2);
            manifestInfo.Model.Variables[variableOneName].ShouldBe(variableOneValue);
            manifestInfo.Model.Variables[variableTwoName].ShouldBe(variableTwoValue);
        }

        [TestMethod]
        public void Load_Import_InvalidPath()
        {
            string manifest =
$@"
{{
  ""includes"": [
    ""invalid.json""
  ],
{s_repoJson}
}}";

            Should.Throw<FileNotFoundException>(() => LoadManifestInfo(manifest));
        }

        [TestMethod]
        public void Load_Import_DuplicateVariables()
        {
            string includeManifestPath = "manifest.variables.json";
            string duplicateVariable = "\"variable1\": \"value1\"";
            string manifest =
$@"
{{
  ""includes"": [
    ""{includeManifestPath}""
  ],
  ""variables"": {{
      {duplicateVariable}
  }},
{s_repoJson}
}}";
            string includeManifest =
$@"
{{
  ""variables"": {{
      {duplicateVariable}
  }}
}}";

            Should.Throw<InvalidOperationException>(() => LoadManifestInfo(manifest, includeManifestPath, includeManifest));
        }

        [TestMethod]
        public void Load_Include_Repos()
        {
            const string includeManifestPath1 = "manifest.custom1.json";
            const string includeManifestPath2 = "manifest.custom2.json";
            string manifest =
$@"
{{
  ""includes"": [
    ""{includeManifestPath1}"",
    ""{includeManifestPath2}""
  ]
}}";

            string includeManifest1 =
$@"
{{
  ""repos"": [
    {CreateRepo("testRepo1", s_dockerfilePath, "testTag1")},
    {CreateRepo("testRepo2", s_dockerfilePath)}
  ]
}}";

            string includeManifest2 =
$@"
{{
  ""repos"": [
    {CreateRepo("testRepo1", s_dockerfilePath, "testTag2")},
    {CreateRepo("testRepo3", s_dockerfilePath)}
  ]
}}";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, manifest);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, includeManifestPath1), includeManifest1);
            File.WriteAllText(Path.Combine(tempFolderContext.Path, includeManifestPath2), includeManifest2);

            DockerfileHelper.CreateDockerfile(s_dockerfilePath, tempFolderContext);

            IManifestOptionsInfo manifestOptions = ManifestHelper.GetManifestOptions(manifestPath);
            ManifestInfo manifestInfo = TestHelper.CreateManifestJsonService().Load(manifestOptions);

            manifestInfo.Model.Repos.Length.ShouldBe(3);
            manifestInfo.Model.Repos[0].Name.ShouldBe("testRepo1");
            manifestInfo.Model.Repos[1].Name.ShouldBe("testRepo2");
            manifestInfo.Model.Repos[2].Name.ShouldBe("testRepo3");

            manifestInfo.Model.Repos[0].Images.Length.ShouldBe(2);
            manifestInfo.Model.Repos[1].Images.ShouldHaveSingleItem();
            manifestInfo.Model.Repos[2].Images.ShouldHaveSingleItem();
        }

        private static ManifestInfo LoadManifestInfo(string manifest, string includeManifestPath = null, string includeManifest = null)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, manifest);

            if (includeManifestPath != null)
            {
                string fullIncludeManifestPath = Path.Combine(tempFolderContext.Path, includeManifestPath);
                File.WriteAllText(fullIncludeManifestPath, includeManifest);
            }

            DockerfileHelper.CreateDockerfile(s_dockerfilePath, tempFolderContext);

            IManifestOptionsInfo manifestOptions = ManifestHelper.GetManifestOptions(manifestPath);
            Mock.Get(manifestOptions).SetupGet(options => options.Variables).Returns(new Dictionary<string, string>());
            return TestHelper.CreateManifestJsonService().Load(manifestOptions);
        }

        [TestMethod]
        public void Load_ImageSyndication_SubstitutesVariables()
        {
            string manifest = $$"""
                {
                  // Manifest comments and trailing commas remain supported.
                  "variables": { "destination": "syndicated-repo" },
                  "repos": [{
                    "name": "repo",
                    "images": [{
                      "syndication": "$(destination)",
                      "platforms": [{
                        "dockerfile": "{{s_dockerfilePath}}",
                        "os": "linux",
                        "osVersion": "trixie",
                        "tags": { "tag": {}, }
                      }]
                    }]
                  }]
                }
                """;

            ImageInfo image = LoadManifestInfo(manifest).AllRepos.Single().AllImages.Single();
            image.SyndicatedRepo.ShouldBe("syndicated-repo");
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("$(empty)")]
        public void Load_ImageSyndication_RejectsEmptyRepository(string destination)
        {
            string manifest = $$"""
                {
                  "variables": { "empty": "" },
                  "repos": [{
                    "name": "repo",
                    "images": [{
                      "syndication": "{{destination}}",
                      "platforms": [{
                        "dockerfile": "{{s_dockerfilePath}}",
                        "os": "linux",
                        "osVersion": "trixie",
                        "tags": { "tag": {} }
                      }]
                    }]
                  }]
                }
                """;

            Should.Throw<ValidationException>(() => LoadManifestInfo(manifest))
                .Message.ShouldContain("syndication");
        }

        [TestMethod]
        [DataRow(false, false, "syndication")]
        [DataRow(true, false, "syndication")]
        [DataRow(false, true, "Syndication")]
        [DataRow(true, true, "Syndication")]
        public void Load_RejectsLegacyTagSyndication(bool sharedTag, bool includedManifest, string memberName)
        {
            string legacyTagMetadata = $$$"""{"{{{memberName}}}": {"repo": "destination"}}""";
            string sharedTagMetadata = sharedTag ? legacyTagMetadata : "{}";
            string platformTagMetadata = sharedTag ? "{}" : legacyTagMetadata;
            string manifest = $$"""
                {
                  "repos": [{
                    "name": "repo",
                    "images": [{
                      "sharedTags": {
                        "shared": {{sharedTagMetadata}}
                      },
                      "platforms": [{
                        "dockerfile": "{{s_dockerfilePath}}",
                        "os": "linux",
                        "osVersion": "trixie",
                        "tags": {
                          "tag": {{platformTagMetadata}}
                        }
                      }]
                    }]
                  }]
                }
                """;

            ValidationException exception = Should.Throw<ValidationException>(() =>
                includedManifest
                    ? LoadManifestInfo("""{"includes": ["legacy.json"]}""", "legacy.json", manifest)
                    : LoadManifestInfo(manifest));
            exception.Message.ShouldContain("Tag-level syndication");
            exception.Message.ShouldContain("image's 'syndication'");
            exception.Message.ShouldContain(includedManifest ? "legacy.json" : "manifest.json");
        }

        private static string CreateRepo(string repoName, string dockerfilePath, string tag = "testTag") =>
$@"
{{
    ""name"": ""{repoName}"",
    ""images"": [
    {{
        ""platforms"": [
        {{
            ""dockerfile"": ""{dockerfilePath}"",
            ""os"": ""linux"",
            ""osVersion"": ""trixie"",
            ""tags"": {{
                ""{tag}"": {{}}
            }}
        }}
        ]
    }}
    ]
}}
";
    }
}
