// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ImageSizeInfo
    {
        public string Id { get; set; }
        public double? AllowedVariance { get; set; }
        public long? BaselineSize { get; set; }
        public long? CurrentSize { get; set; }
        public bool ImageExistsOnDisk { get; set; }
        public double? MaxVariance => BaselineSize + AllowedVariance;
        public double? MinVariance => BaselineSize - AllowedVariance;
        public long? SizeDifference => CurrentSize - BaselineSize;
        public bool WithinAllowedVariance => BaselineSize.HasValue && AllowedVariance >= Math.Abs(SizeDifference.Value);
    }
}
