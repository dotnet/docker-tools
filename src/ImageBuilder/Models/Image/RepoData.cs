// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Image
{
    public class RepoData : IComparable<RepoData>
    {
        [JsonProperty(Required = Required.Always)]
        public string Repo { get; set; }

        public List<ImageData> Images { get; set; } = new List<ImageData>();

        public int CompareTo([AllowNull] RepoData other)
        {
            if (other is null)
            {
                return 1;
            }

            return Repo.CompareTo(other.Repo);
        }
    }
}
