// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using LibGit2Sharp;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IGitService
    {
        string GetCommitSha(string filePath, bool useFullHash = false);

        /// <summary>
        /// Gets the absolute path to the root of the Git repository that contains the given path.
        /// </summary>
        /// <param name="path">
        /// An absolute path to a file or directory that resides within a Git repository's working tree.
        /// </param>
        /// <returns>The absolute path to the containing repository's root directory.</returns>
        string GetRepoRoot(string path);

        IRepository CloneRepository(string sourceUrl, string workdirPath, CloneOptions options);

        void Stage(IRepository repository, string path);
    }
}
