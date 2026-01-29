// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Annotations
{
    public class EolAnnotationsData
    {
        public EolAnnotationsData()
        {
        }

        public EolAnnotationsData(List<EolDigestData> eolDigests, DateOnly? eolDate = null)
        {
            EolDate = eolDate;
            EolDigests = eolDigests;
        }

        public DateOnly? EolDate { get; set; }

        public List<EolDigestData> EolDigests { get; set; } = [];
    }
}
