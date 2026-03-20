#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest;

[Description(
    "A description of where a tag should be syndicated to."
    )]
public class TagSyndication
{
    [Description(
        "Name of the repo to syndicate the tag to."
    )]
    public string Repo { get; set; }

    [Description(
        "List of destination tag names to syndicate the tag to."
    )]
    public string[] DestinationTags { get; set; }
}
