﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IGitService))]
    public class GitService : IGitService
    {
        public string GetCommitSha(string filePath, bool useFullHash = false)
        {
            return GitHelper.GetCommitSha(filePath, useFullHash);
        }
    }
}
