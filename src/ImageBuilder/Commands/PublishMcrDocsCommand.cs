// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Mcr;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class PublishMcrDocsCommand(
    IManifestJsonService manifestJsonService,
    IGitService gitService,
    IGitHubClientFactory gitHubClientFactory,
    ILogger<PublishMcrDocsCommand> logger)
        : ManifestCommand<PublishMcrDocsOptions>(manifestJsonService)
{
    private const string McrTagsPlaceholder = "Tags go here.";

    protected override string Description => "Publishes the readmes to MCR";

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("PUBLISHING MCR DOCS");

        ValidateReadmeFilenames(Manifest);

        // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        List<GitObject> gitObjects = [];
        gitObjects.AddRange(GetUpdatedReadmes());
        gitObjects.AddRange(GetUpdatedTagsMetadata());

        foreach (GitObject gitObject in gitObjects)
        {
            logger.LogInformation(
                "Updated file '{Path}' with contents: {Content}",
                gitObject.Path,
                gitObject.Content);
        }

        if (!Options.IsDryRun)
        {
            using IGitHubClient gitHubClient =
                await gitHubClientFactory.GetClientAsync(Options.GitOptions, Options.IsDryRun, cancellationToken);

            await RetryHelper.GetWaitAndRetryPolicy<HttpRequestException>(logger).ExecuteAsync(
                action: async actionCt =>
                {
                    GitReference gitRef = await GitHelper.PushChangesAsync(
                        gitHubClient,
                        Options,
                        "Mirroring product readmes",
                        (branch, innerCt) => FilterUpdatedGitObjectsAsync(gitObjects, gitHubClient, branch, innerCt),
                        actionCt);

                    if (gitRef != null)
                    {
                        logger.LogInformation(
                            "{PipelineOutputVariable}",
                            PipelineHelper.FormatOutputVariable("readmeCommitDigest", gitRef.Object.Sha));
                    }
                },
                cancellationToken);
        }
    }

    private bool PathIsValid(Readme readme) =>
        Options.RootPath is null || Path.GetFullPath(readme.Path).StartsWith(Path.GetFullPath(Options.RootPath));

    private void ValidateReadmeFilenames(ManifestInfo manifest)
    {
        // Readme filenames must be unique within each product directory because source paths are flattened in mcrdocs.

        var readmePathsWithDuplicateFilenames = manifest.AllRepos
            .SelectMany(repo => repo.Readmes
                .Where(PathIsValid)
                .Select(readme => new
                {
                    readme.Path,
                    TargetPath = string.Join('/', GetProductRepo(repo), Path.GetFileName(readme.Path))
                }))
            .GroupBy(readme => readme.TargetPath)
            .Where(group => group.Count() > 1);

        if (readmePathsWithDuplicateFilenames.Any())
        {
            IEnumerable<string> errorMessages = readmePathsWithDuplicateFilenames
                .Select(group =>
                    $"""
                    Readme filenames must be unique within each MCR docs product directory. The following readme paths resolve to '{group.Key}':
                    {string.Join(Environment.NewLine, group.Select(readme => readme.Path))}
                    """);

            throw new ValidationException(string.Join(Environment.NewLine + Environment.NewLine, errorMessages.ToArray()));
        }
    }

    private async Task<IEnumerable<GitObject>> FilterUpdatedGitObjectsAsync(
        IEnumerable<GitObject> gitObjects,
        IGitHubClient gitHubClient,
        GitHubBranch branch,
        CancellationToken cancellationToken)
    {
        List<GitObject> updatedGitObjects = [];
        foreach (GitObject gitObject in gitObjects)
        {
            string currentContent = await gitHubClient.GetGitHubFileContentsAsync(gitObject.Path, branch);

            // Manually check for cancellation because GetGitHubFileContentsAsync does not accept cancellation token
            cancellationToken.ThrowIfCancellationRequested();

            if (currentContent == gitObject.Content)
            {
                logger.LogInformation("File '{Path}' has not changed.", gitObject.Path);
            }
            else
            {
                logger.LogInformation("File '{Path}' has changed.", gitObject.Path);
                updatedGitObjects.Add(gitObject);
            }
        }

        return updatedGitObjects;
    }

    private GitObject GetGitObject(string repo, string filePath, string updatedContent)
    {
        // We only use the filename from the provided file path because all files in the target mcrdocs repo
        // are located at the root of the repo directory.
        string gitPath = string.Join('/', Options.GitOptions.Path, repo, Path.GetFileName(filePath));

        return new GitObject
        {
            Path = gitPath,
            Type = GitObject.TypeBlob,
            Mode = GitObject.ModeFile,
            Content = updatedContent
        };
    }

    private string GetProductRepo(RepoInfo repo)
    {
        string repoName = repo.QualifiedName.TrimStartString($"{Manifest.Registry}/");
        return repoName.Substring(0, repoName.LastIndexOf('/'));
    }

    private List<GitObject> GetUpdatedReadmes()
    {
        List<GitObject> readmes = [];

        foreach (RepoInfo repo in Manifest.FilteredRepos)
        {
            string productRepo = GetProductRepo(repo);
            IEnumerable<GitObject> productReadmes = repo.Readmes
                .Where(PathIsValid)
                .Select(readme => GetReadmeGitObject(productRepo, readme.Path));

            readmes.AddRange(productReadmes);
        }

        if (!string.IsNullOrEmpty(Manifest.ReadmePath) && !Options.ExcludeProductFamilyReadme)
        {
            string productRepo = GetProductRepo(Manifest.AllRepos.First());
            readmes.Add(GetReadmeGitObject(productRepo, Manifest.ReadmePath));
        }

        return readmes;
    }

    private GitObject GetReadmeGitObject(string productRepo, string readmePath)
    {
        string updatedReadMe = File.ReadAllText(readmePath);
        updatedReadMe = ReadmeHelper.UpdateTagsListing(updatedReadMe, McrTagsPlaceholder);
        return GetGitObject(productRepo, readmePath, updatedReadMe);
    }

    private List<GitObject> GetUpdatedTagsMetadata()
    {
        List<GitObject> metadata = [];

        foreach (RepoInfo repo in Manifest.FilteredRepos)
        {
            string updatedMetadata = McrTagsMetadataGenerator.Execute(Manifest, repo, generateGitHubLinks: true, gitService, Options.SourceRepoUrl);
            string metadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplate);
            metadata.Add(GetGitObject(GetProductRepo(repo), metadataFileName, updatedMetadata));
        }

        return metadata;
    }
}
