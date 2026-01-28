// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    public static class AzdoGitHttpClientExtensions
    {
        public static async Task<bool> FileExistsAsync(this IAzdoGitHttpClient gitHttpClient, Guid repositoryId, string branchName, string path)
        {
            try
            {
                await gitHttpClient.GetItemAsync(repositoryId, path, versionDescriptor: new GitVersionDescriptor
                {
                    Version = branchName,
                    VersionType = GitVersionType.Branch
                });
                return true;
            }
            catch (VssServiceException)
            {
                return false;
            }
        }

        public static async Task<GitPush> PushChangesAsync(this IAzdoGitHttpClient gitHttpClient, string commitMessage, Guid repositoryId, GitRef branchRef,
            IDictionary<string, string> files)
        {
            GitRefUpdate branchRefUpdate = new GitRefUpdate
            {
                Name = branchRef.Name,
                OldObjectId = branchRef.ObjectId
            };

            string branchName = branchRef.Name.Substring(branchRefUpdate.Name.LastIndexOf('/') + 1);

            var fileInfos = files
                .Select(kvp => new
                {
                    Exists = FileExistsAsync(gitHttpClient, repositoryId, branchName, kvp.Key),
                    Path = kvp.Key,
                    FileContents = kvp.Value
                })
                .ToArray();

            // Wait for all of the operations that check for file existence to complete
            await Task.WhenAll(fileInfos.Select(info => info.Exists));

            GitChange[] changes = fileInfos
                .Select(info =>
                    new GitChange
                    {
                        ChangeType = info.Exists.Result ? VersionControlChangeType.Edit : VersionControlChangeType.Add,
                        Item = new GitItem
                        {
                            Path = info.Path
                        },
                        NewContent = new ItemContent
                        {
                            Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(info.FileContents)),
                            ContentType = ItemContentType.Base64Encoded
                        }
                    })
                .ToArray();

            GitCommitRef commitRef = new GitCommitRef
            {
                Comment = commitMessage,
                Changes = changes
            };

            GitPush push = new GitPush
            {
                RefUpdates = new GitRefUpdate[] { branchRefUpdate },
                Commits = new GitCommitRef[] { commitRef }
            };

            return await gitHttpClient.CreatePushAsync(push, repositoryId);
        }
    }
}
