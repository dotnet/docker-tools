// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateReadmesCommand : GenerateArtifactsCommand<GenerateReadmesOptions, GenerateReadmesOptionsBuilder>
    {
        private const string ArtifactName = "Readme";
        private const string McrTagsRenderingToolTag = "mcr.microsoft.com/mcr/renderingtool:1.0";

        private readonly IGitService _gitService;

        [ImportingConstructor]
        public GenerateReadmesCommand(IEnvironmentService environmentService, IGitService gitService) : base(environmentService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        }

        protected override string Description =>
            "Generates the Readmes from the Cottle based templates (http://r3c.github.io/cottle/) and updates the tag listing section";

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING READMES");

            // Generate Product Family Readme
            await GenerateArtifactsAsync(
                new ManifestInfo[] { Manifest },
                (manifest) => manifest.ReadmeTemplatePath,
                (manifest) => manifest.ReadmePath,
                (manifest) => GetSymbols(manifest),
                nameof(Models.Manifest.Manifest.ReadmeTemplate),
                ArtifactName);

            // Generate Repo Readmes
            await GenerateArtifactsAsync(
                Manifest.FilteredRepos,
                (repo) => repo.ReadmeTemplatePath,
                (repo) => repo.ReadmePath,
                (repo) => GetSymbols(repo),
                nameof(Models.Manifest.Repo.ReadmeTemplate),
                ArtifactName,
                (readme, repo) => UpdateTagsListing(readme, repo));

            ValidateArtifacts();
        }

        public Dictionary<Value, Value> GetSymbols(ManifestInfo manifest) =>
            GetCommonSymbols(manifest.ReadmeTemplatePath, manifest, (manifest) => GetSymbols(manifest));

        public Dictionary<Value, Value> GetSymbols(RepoInfo repo)
        {
            Dictionary<Value, Value> symbols = GetCommonSymbols(repo.ReadmeTemplatePath, repo, (repo) => GetSymbols(repo));
            symbols["FULL_REPO"] = repo.QualifiedName;
            symbols["REPO"] = repo.Name;
            symbols["PARENT_REPO"] = GetParentRepoName(repo);
            symbols["SHORT_REPO"] = GetShortRepoName(repo);

            return symbols;
        }

        private Dictionary<Value, Value> GetCommonSymbols<TContext>(
            string sourceTemplatePath,
            TContext context,
            Func<TContext, IReadOnlyDictionary<Value, Value>> getSymbols)
        {
            Dictionary<Value, Value> symbols = GetSymbols();
            symbols["IS_PRODUCT_FAMILY"] = context is ManifestInfo;
            symbols["InsertTemplate"] = Value.FromFunction(Function.CreatePure1((state, path) =>
                RenderTemplateAsync(Path.Combine(Path.GetDirectoryName(sourceTemplatePath), path.AsString), context, getSymbols).Result));

            return symbols;
        }

        private string GetParentRepoName(RepoInfo repo)
        {
            string[] parts = repo.Name.Split('/');
            return parts.Length > 1 ? parts[parts.Length - 2] : string.Empty;
        }

        private string GetShortRepoName(RepoInfo repo)
        {
            int lastSlashIndex = repo.Name.LastIndexOf('/');
            return lastSlashIndex == -1 ? repo.Name : repo.Name.Substring(lastSlashIndex + 1);
        }

        private string UpdateTagsListing(string readme, RepoInfo repo)
        {
            if (repo.Model.McrTagsMetadataTemplate == null)
            {
                return readme;
            }

            string tagsMetadata = McrTagsMetadataGenerator.Execute(
                _gitService, Manifest, repo, Options.SourceRepoUrl, Options.SourceRepoBranch);
            string tagsListing = GenerateTagsListing(repo, tagsMetadata);
            return ReadmeHelper.UpdateTagsListing(readme, tagsListing);
        }

        private string GenerateTagsListing(RepoInfo repo, string tagsMetadata)
        {
            Logger.WriteSubheading("GENERATING TAGS LISTING");

            string tagsDoc;

            string tempDir = $"{this.GetCommandName()}-{DateTime.Now.ToFileTime()}";
            Directory.CreateDirectory(tempDir);

            try
            {
                string tagsMetadataFileName = Path.GetFileName(repo.Model.McrTagsMetadataTemplate);
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

                    try
                    {
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
                        ExecuteHelper.Execute("docker", $"container rm -f {renderingToolId}", Options.IsDryRun);
                    }
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
    }
}
