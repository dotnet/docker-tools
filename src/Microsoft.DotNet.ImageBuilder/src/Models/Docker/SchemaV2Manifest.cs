// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Models.Docker
{
    public partial class SchemaV2Manifest
    {
        public long SchemaVersion { get; set; }

        public string MediaType { get; set; }

        public Descriptor Config { get; set; }

        public Descriptor[] Layers { get; set; }
    }
}
