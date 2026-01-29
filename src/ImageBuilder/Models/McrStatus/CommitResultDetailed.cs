#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.McrStatus
{
    public class CommitResultDetailed
    {
        [JsonProperty(PropertyName = "Id")]
        public string OnboardingRequestId { get; set; }
        public DateTime QueueTime { get; set; }
        public string CommitDigest { get; set; }
        public string Branch { get; set; }
        public List<string> ContentFiles { get; set; } = new List<string>();
        public StageStatus OverallStatus { get; set; }
        public ContentSubstatus Substatus { get; set; } = new ContentSubstatus();
    }
}
