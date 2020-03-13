// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class ImageArtifactDetails
    {
        private const string schemaVersion = "1.0";

        public string SchemaVersion
        {
            get { return schemaVersion; }
            set { }
        }

        public List<RepoData> Repos { get; set; } = new List<RepoData>();
    }
}
