// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Acr
{
    public class GetTagsResponse
    {
        public string ImageName { get; set; }
        public string Registry { get; set; }
        public List<TagDetails> Tags { get; set; }
    }
}
