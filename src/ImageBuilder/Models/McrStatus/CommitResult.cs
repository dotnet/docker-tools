#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.McrStatus
{
    public class CommitResult
    {
        public string CommitDigest { get; set; }
        public string Branch { get; set; }
        public List<string> ContentFiles { get; set; } = new List<string>();
        public List<CommitStatus> Value { get; set; } = new List<CommitStatus>();
    }
}
