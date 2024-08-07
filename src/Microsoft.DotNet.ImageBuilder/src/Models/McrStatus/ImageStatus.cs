// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.McrStatus
{
    public class ImageStatus
    {
        [JsonProperty(PropertyName = "Id")]
        public string OnboardingRequestId { get; set; } = string.Empty;
        public string SourceRepository { get; set; } = string.Empty;
        public string TargetRepository { get; set; } = string.Empty;
        public DateTime QueueTime { get; set; }
        public string? Tag { get; set; }
        public StageStatus OverallStatus { get; set; }
        public string? FailureReason { get; set; }
    }
}
