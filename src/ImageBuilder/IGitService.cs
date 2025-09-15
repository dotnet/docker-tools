// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using LibGit2Sharp;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IGitService
    {
        string GetCommitSha(string filePath, bool useFullHash = false);

        IRepository CloneRepository(string sourceUrl, string workdirPath, CloneOptions options);

        void Stage(IRepository repository, string path);
    }
}
