// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Models.Docker
{
    public partial class Manifest
    {
        public string Ref { get; set; }

        public Descriptor Descriptor { get; set; }

        public SchemaV2Manifest SchemaV2Manifest { get; set; }
    }
}
