// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using LibGit2Sharp;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IGitService))]
    [method: ImportingConstructor]
    public class GitService(ILoggerService logger) : IGitService
    {
        private readonly ILoggerService _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public string GetCommitSha(string filePath, bool useFullHash = false)
        {
            return GitHelper.GetCommitSha(filePath, useFullHash);
        }

        public IRepository CloneRepository(string sourceUrl, string workdirPath, CloneOptions options)
        {
            _logger.WriteMessage($"Cloning repository {sourceUrl} to {workdirPath}");
            Repository.Clone(sourceUrl, workdirPath, options);
            _logger.WriteMessage($"Cloned repository {sourceUrl} to {workdirPath}");
            return new Repository(workdirPath);
        }

        public void Stage(IRepository repository, string path)
        {
            // The Stage method is encapsulated in this service in order for it to be mockable by unit tests.
            // Due to the Stage method's dependency on the Diff class, it prevents it from being easily used
            // with its default implementation due to https://github.com/libgit2/libgit2sharp/issues/1856.

            LibGit2Sharp.Commands.Stage(repository, path);
        }
    }
}
