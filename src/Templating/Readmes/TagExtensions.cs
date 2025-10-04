// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.DockerTools.Templating.Readmes;

internal static class TagExtensions
{
    extension(Tag tag)
    {
        public bool IsDocumented => tag.DocType switch
        {
            TagDocumentationType.Undocumented => false,
            _ => true
        };
    }
}
