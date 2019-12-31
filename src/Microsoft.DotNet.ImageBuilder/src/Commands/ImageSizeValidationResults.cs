// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ImageSizeValidationResults
    {
        public ImageSizeValidationResults(
            IEnumerable<ImageSizeInfo> imagesWithNoSizeChange,
            IEnumerable<ImageSizeInfo> imagesWithAllowedSizeChange,
            IEnumerable<ImageSizeInfo> imagesWithDisallowedSizeChange,
            IEnumerable<ImageSizeInfo> imagesWithMissingBaseline,
            IEnumerable<ImageSizeInfo> imagesWithExtraneousBaseline)
        {
            ImagesWithNoSizeChange = imagesWithNoSizeChange;
            ImagesWithAllowedSizeChange = imagesWithAllowedSizeChange;
            ImagesWithDisallowedSizeChange = imagesWithDisallowedSizeChange;
            ImagesWithMissingBaseline = imagesWithMissingBaseline;
            ImagesWithExtraneousBaseline = imagesWithExtraneousBaseline;
        }

        public IEnumerable<ImageSizeInfo> ImagesWithNoSizeChange { get; }
        public IEnumerable<ImageSizeInfo> ImagesWithAllowedSizeChange { get; }
        public IEnumerable<ImageSizeInfo> ImagesWithDisallowedSizeChange { get; }
        public IEnumerable<ImageSizeInfo> ImagesWithMissingBaseline { get; }
        public IEnumerable<ImageSizeInfo> ImagesWithExtraneousBaseline { get; }
    }
}
