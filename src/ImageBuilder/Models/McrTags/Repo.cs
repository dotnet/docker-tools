#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Microsoft.DotNet.ImageBuilder.Models.McrTags
{
    public class Repo
    {
        [YamlMember(Alias = "repoName")]
        public string Name { get; set; }

        public bool CustomTablePivots { get; set; }

        public List<TagGroup> TagGroups { get; set; }
    }
}
