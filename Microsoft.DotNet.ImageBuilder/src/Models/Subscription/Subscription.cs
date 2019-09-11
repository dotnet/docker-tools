// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Subscription
{
    public class Subscription
    {
        [JsonProperty(Required = Required.Always)]
        public GitRepo RepoInfo { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string ManifestPath { get; set; }

        [JsonProperty(Required = Required.Always)]
        public PipelineTrigger PipelineTrigger { get; set; }

        public string OsType { get; set; }

        public string Id => $"{RepoInfo.Owner}/{RepoInfo.Name}/{RepoInfo.Branch}/{ManifestPath}";

        public override string ToString() => Id;
    }
}
