// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class ImageData
    {
        public SortedDictionary<string, PlatformData> Platforms { get; set; } =
            new SortedDictionary<string, PlatformData>();

        /// <summary>
        /// Gets or sets a reference to the corresponding image definition in the manifest.
        /// </summary>
        [JsonIgnore]
        public ImageInfo ManifestImage { get; set; }
    }
}
