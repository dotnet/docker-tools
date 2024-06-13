// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.EolAnnotations
{
    public class EolAnnotationsData
    {
        public EolAnnotationsData()
        {
        }

        public EolAnnotationsData(DateOnly eolDate, List<EolDigestData> eolDigests)
        {
            EolDate = eolDate;
            EolDigests = eolDigests;
        }

        [JsonProperty(Required = Required.Always)]
        public DateOnly EolDate { get; set; }

        public List<EolDigestData>? EolDigests { get; set; }
    }
}
#nullable disable
