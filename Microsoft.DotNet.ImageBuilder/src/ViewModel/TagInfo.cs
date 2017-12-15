// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class TagInfo
    {
        public string DockerfilePath { get; set; }
        public string FullyQualifiedName { get; private set; }
        public Tag Model { get; private set; }
        public string Name { get; private set; }
        private TagInfo()
        {
        }

        public static TagInfo Create(string name, Tag model, Manifest manifest, string repoName, string dockerfilePath = "")
        {
            TagInfo tagInfo = new TagInfo();
            tagInfo.Model = model;
            tagInfo.DockerfilePath = dockerfilePath;
            tagInfo.Name = Utilities.SubstituteVariables(manifest.TagVariables, name, tagInfo.GetDockerfileGitCommitSha);
            tagInfo.FullyQualifiedName = $"{repoName}:{tagInfo.Name}";

            return tagInfo;
        }

        public string GetDockerfileGitCommitSha(string variableName)
        {
            string commitSha = null;
            if (variableName == "DockerfileGitCommitSha")
            {
                commitSha = Utilities.GetAbbreviatedCommitSha(DockerfilePath);
            }
            return commitSha;
        }
    }
}
