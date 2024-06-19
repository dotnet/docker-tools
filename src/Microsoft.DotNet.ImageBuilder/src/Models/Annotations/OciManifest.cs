// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.EolAnnotations
{
    public class OciManifest
    {
        public string? Reference { get; set; }
        public string? MediaType { get; set; }
        public string? Digest { get; set; }
        public int? Size { get; set; }
        public IDictionary<string, string>? Annotations { get; set; }
        public string? ArtifactType { get; set; }

        public OciManifest()
        {
        }
    }
}

