#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    public static class BuildExtensions
    {
        public static string GetWebLink(this TeamFoundation.Build.WebApi.Build build) =>
            ((ReferenceLink)build.Links.Links["web"]).Href;
    }
}
