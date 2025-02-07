// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder
{
    public class ImageInfoMergeOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the image info merge is occurring as part of publishing
        /// the image info file. There is different merge logic involved depending on whether it's merging
        /// two image info files as part of the build versus merging a consolidated image info file into
        /// a previously published version (the publish scenario). For example, for the publish scenario,
        /// existing tag values are replaced rather than merged.
        /// </summary>
        public bool IsPublish { get; set; }
    }
}
