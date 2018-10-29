// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Model
{
    public class Tag
    {
        public string DocumentationGroup { get; set; }

        public string Id { get; set; }

        public bool IsLocal { get; set; }

        public bool IsUndocumented { get; set; }

        public Tag()
        {
        }
    }
}
