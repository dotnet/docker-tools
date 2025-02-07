// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Manifest
{
    public enum TagDocumentationType
    {
        /// <summary>
        /// The tag is always documented.
        /// </summary>
        Documented,

        /// <summary>
        /// The tag is never documented.
        /// </summary>
        Undocumented,

        /// <summary>
        /// The tag is only documented if there are corresponding platform tags that are documented.
        /// </summary>
        PlatformDocumented
    }
}
