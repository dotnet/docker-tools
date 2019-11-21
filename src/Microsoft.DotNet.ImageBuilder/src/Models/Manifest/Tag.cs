// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest
{
    [Description(
        "A tag object contains metadata about a Docker tag. It is a JSON object " +
        "with its tag name used as the attribute name."
        )]
    public class Tag
    {
        [Description(
            "An identifier used to conceptually group related tags in the readme " +
            "documentation."
            )]
        public string DocumentationGroup { get; set; }

        [Description(
            "Indicates whether the image should only be tagged with this tag on the " +
            "local machine that builds the image. The published image will not include " +
            "this tag. This is only used for advanced build dependency scenarios."
            )]
        public bool IsLocal { get; set; }

        [Description(
            "Indicates whether this tag should not be documented in the readme file. The " +
            "image will still be tagged with this tag however and will still be published. " +
            "This is useful when deprecating a tag that still needs to be kept up-to-date " +
            "but not wanting it documented."
            )]
        public bool IsUndocumented { get; set; }

        public Tag()
        {
        }
    }
}
