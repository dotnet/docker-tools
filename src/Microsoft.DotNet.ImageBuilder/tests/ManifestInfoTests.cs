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
    {{
      ""name"": ""testRepo"",
      ""images"": [
        {{
          ""platforms"": [
            {{
              ""dockerfile"": ""{s_dockerfilePath}"",
              ""os"": ""linux"",
              ""osVersion"": ""stretch"",
              ""tags"": {{
                  ""testTag"": {{}}
              }}
            }}
          ]
        }}
      ]
    }}
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
            return ManifestInfo.Load(manifestOptions);
        }
    }
}
