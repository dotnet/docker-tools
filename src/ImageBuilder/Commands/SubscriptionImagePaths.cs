// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class SubscriptionImagePaths
    {
        public string SubscriptionId { get; set; }

        public string[] ImagePaths { get; set; }
    }
}
