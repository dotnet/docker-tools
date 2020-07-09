// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.McrStatus
{
    public class ImageStatus
    {
        [JsonProperty(PropertyName = "Id")]
        public string OnboardingRequestId { get; set; }
        public string SourceRepository { get; set; }
        public string TargetRepository { get; set; }
        public DateTime QueueTime { get; set; }
        public string Tag { get; set; }
        public StageStatus OverallStatus { get; set; }
    }
}
