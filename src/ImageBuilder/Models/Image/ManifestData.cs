// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Image
{
    public class ManifestData
    {
        public string Digest { get; set; }
        public List<string> SyndicatedDigests { get; set; } = new();
        public DateTime Created { get; set; }
        public List<string> SharedTags { get; set; } = new();
    }
}
