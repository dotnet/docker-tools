// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ImageModel
{
    public class RepoData
    {
        [JsonProperty(Required = Required.Always)]
        public string Repo { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, ImageData> Images { get; set; }

        public static List<RepoData> SortRepoData(List<RepoData> repos)
        {
            repos = repos.OrderBy(r => r.Repo).ToList();
            foreach (RepoData repo in repos.Where(r => r.Images != null))
            {
                repo.Images = repo.Images.Sort();

                foreach (ImageData image in repo.Images.Values)
                {
                    image.BaseImageDigests = image.BaseImageDigests?.Sort();
                }
            }

            return repos;
        }
    }
}
