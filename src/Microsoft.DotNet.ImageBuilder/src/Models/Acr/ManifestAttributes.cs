﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.ImageBuilder.Models.Acr
{
    public class ManifestAttributes
    {
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Digest { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}
