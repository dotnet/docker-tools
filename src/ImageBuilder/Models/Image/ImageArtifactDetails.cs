// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Image
{
    public class ImageArtifactDetails
    {
        public string SchemaVersion
        {
            get { return "1.0"; }
            set { }
        }

        public List<RepoData> Repos { get; set; } = new List<RepoData>();
    }
}
