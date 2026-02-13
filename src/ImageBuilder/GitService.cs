// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using LibGit2Sharp;

namespace Microsoft.DotNet.ImageBuilder
{
    public class GitService(ILogger<GitService> logger) : IGitService
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public string GetCommitSha(string filePath, bool useFullHash = false)
        {
            return GitHelper.GetCommitSha(filePath, useFullHash);
        }

        public IRepository CloneRepository(string sourceUrl, string workdirPath, CloneOptions options)
        {
            _logger.LogInformation($"Cloning repository {sourceUrl} to {workdirPath}");
            Repository.Clone(sourceUrl, workdirPath, options);
            return new Repository(workdirPath);
        }

        public void Stage(IRepository repository, string path)
        {
            // The Stage method is encapsulated in this service in order for it to be mockable by unit tests.
            // Due to the Stage method's dependency on the Diff class, it prevents it from being easily used
            // with its default implementation due to https://github.com/libgit2/libgit2sharp/issues/1856.

            _logger.LogInformation($"Staging {path} in repository {repository.Info.WorkingDirectory}");
            LibGit2Sharp.Commands.Stage(repository, path);
        }
    }
}
