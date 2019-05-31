// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.SubscriptionsModel
{
    public class Subscription
    {
        [JsonProperty(Required = Required.Always)]
        public GitRepoInfo RepoInfo { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string ManifestPath { get; set; }

        [JsonProperty(Required = Required.Always)]
        public PipelineTrigger PipelineTrigger { get; set; }

        public override string ToString()
        {
            return $"{RepoInfo.Owner}/{RepoInfo.Name}/{RepoInfo.Branch}/{ManifestPath}";
        }
    }
}
