// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateTagsReadmeCommand : ManifestCommand<GenerateTagsReadmeOptions>
    {
        private const string McrTagsRenderingToolTag = "mcr.microsoft.com/mcr/renderingtool:1.0";
        private readonly IEnvironmentService _environmentService;
        private readonly IGitService _gitService;

        [ImportingConstructor]
        public GenerateTagsReadmeCommand(IEnvironmentService environmentService, IGitService gitService) : base()
        {
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        }

        public override Task ExecuteAsync()
        {
            if (Manifest.FilteredRepos.Count() != 1)
            {
                throw new InvalidOperationException(
                    $"{Options.GetCommandName()} only supports generating the tags readme for one repo per invocation.");
            }

            RepoInfo repo = Manifest.FilteredRepos.First();
            string tagsMetadata = McrTagsMetadataGenerator.Execute(
                _gitService, Manifest, repo, Options.SourceRepoUrl, Options.SourceRepoBranch);
            string tagsListing = GenerateTagsListing(repo, tagsMetadata);
            UpdateReadme(repo, tagsListing);

            return Task.CompletedTask;
        }

        private string GenerateTagsListing(RepoInfo repo, string tagsMetadata)
        {
            Logger.WriteHeading("GENERATING TAGS LISTING");

            string tagsDoc;

            string tempDir = $"{Options.GetCommandName()}-{DateTime.Now.ToFileTime()}";
            Directory.CreateDirectory(tempDir);

            try
            {
                string tagsMetadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplatePath);
                File.WriteAllText(
                    Path.Combine(tempDir, tagsMetadataFileName),
                    tagsMetadata);

                string dockerfilePath = Path.Combine(tempDir, "Dockerfile");
                File.WriteAllText(
                    dockerfilePath,
                    $"FROM {McrTagsRenderingToolTag}{Environment.NewLine}COPY {tagsMetadataFileName} /tableapp/files/ ");

                string renderingToolId = $"renderingtool-{DateTime.Now.ToFileTime()}";
                DockerHelper.PullImage(McrTagsRenderingToolTag, Options.IsDryRun);
                ExecuteHelper.Execute(
                    "docker",
                    $"build -t {renderingToolId} -f {dockerfilePath} {tempDir}",
                    Options.IsDryRun);

                try
                {
                    ExecuteHelper.Execute(
                        "docker",
                        $"run --name {renderingToolId} {renderingToolId} {tagsMetadataFileName}",
                        Options.IsDryRun);

                    string outputPath = Path.Combine(tempDir, "output.md");
                    ExecuteHelper.Execute(
                        "docker",
                        $"cp {renderingToolId}:/tableapp/files/{repo.Name.Replace('/', '-')}.md {outputPath}",
                        Options.IsDryRun
                    );

                    tagsDoc = File.ReadAllText(outputPath);
                }
                finally
                {
                    ExecuteHelper.Execute("docker", $"image rm -f {renderingToolId}", Options.IsDryRun);
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }

            if (Options.IsVerbose)
            {
                Logger.WriteSubheading($"Tags Documentation:");
                Logger.WriteMessage(tagsDoc);
            }

            return tagsDoc;
        }

        public void UpdateReadme(RepoInfo repo, string tagsListing)
        {
            Logger.WriteHeading("UPDATING README");

            string currentReadme = File.ReadAllText(repo.Model.ReadmePath);
            string updatedReadme = ReadmeHelper.UpdateTagsListing(currentReadme, tagsListing);

            if (currentReadme == updatedReadme)
            {
                Logger.WriteMessage($"The tag listing in '{repo.Model.ReadmePath}' is up-to-date.");
            }
            else if (Options.Validate)
            {
                Logger.WriteError($"The tag listing in '{repo.Model.ReadmePath}' is out of date.");
                _environmentService.Exit(1);
            }
            else
            {
                File.WriteAllText(repo.Model.ReadmePath, updatedReadme);

                Logger.WriteSubheading($"Updated the tag listing in '{repo.Model.ReadmePath}'");
            }

            Logger.WriteMessage();
        }
    }
}
