// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Subscription
{
    public class SubscriptionManifest : GitFile
    {
        public Dictionary<string, string> Variables { get; set; } = new();
    }
}
