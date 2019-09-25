// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace FilePusher.Models
{
    public class Config
    {
        [JsonProperty(Required = Required.Always)]
        public string CommitMessage { get; set; }

        public string PullRequestDescription { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string PullRequestTitle { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string SourcePath { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string WorkingBranchSuffix { get; set; }

        public GitRepo[] Repos { get; set; }
    }
}
