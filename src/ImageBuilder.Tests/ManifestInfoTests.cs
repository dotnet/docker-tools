#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class ManifestInfoTests
    {
        private static string s_dockerfilePath = "testDockerfile";
        private static string s_repoJson =
$@"
  ""repos"": [
    {CreateRepo("testRepo", s_dockerfilePath)}
  ]
";

        [Fact]
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
            Assert.Equal(2, manifestInfo.Model.Variables.Count);
            Assert.Equal(variableOneValue, manifestInfo.Model.Variables[variableOneName]);
            Assert.Equal(variableTwoValue, manifestInfo.Model.Variables[variableTwoName]);
        }

        [Fact]
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

            Assert.Throws<FileNotFoundException>(() => LoadManifestInfo(manifest));
        }

        [Fact]
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

            Assert.Throws<InvalidOperationException>(() => LoadManifestInfo(manifest, includeManifestPath, includeManifest));
        }

        [Fact]
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

            Assert.Equal(3, manifestInfo.Model.Repos.Length);
            Assert.Equal("testRepo1", manifestInfo.Model.Repos[0].Name);
            Assert.Equal("testRepo2", manifestInfo.Model.Repos[1].Name);
            Assert.Equal("testRepo3", manifestInfo.Model.Repos[2].Name);

            Assert.Equal(2, manifestInfo.Model.Repos[0].Images.Length);
            Assert.Single(manifestInfo.Model.Repos[1].Images);
            Assert.Single(manifestInfo.Model.Repos[2].Images);
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
            return TestHelper.CreateManifestJsonService().Load(manifestOptions);
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
