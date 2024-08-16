// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.McrTags;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateReadmesCommand : GenerateArtifactsCommand<GenerateReadmesOptions, GenerateReadmesOptionsBuilder>
    {
        private const string ArtifactName = "Readme";
        private const string LinuxTableHeader = "Tags | Dockerfile | OS Version\n-----------| -------------| -------------";
        private const string WindowsTableHeader = "Tag | Dockerfile\n---------| ---------------";

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
                (manifest, templatePath, indent) => GetTemplateState(manifest, templatePath, indent),
                nameof(Readme.TemplatePath),
                ArtifactName);

            // Generate Repo Readmes
            await GenerateArtifactsAsync(
                Manifest.FilteredRepos
                    .Select(repo => repo.Readmes.Select(readme => (repo, readme)))
                    .SelectMany(repoReadme => repoReadme),
                ((RepoInfo repo, Readme readme) context) => context.readme.TemplatePath,
                ((RepoInfo repo, Readme readme) context) => context.readme.Path,
                ((RepoInfo repo, Readme readme) context, string templatePath, string indent) => GetTemplateState(context.repo, templatePath, indent),
                nameof(Readme.TemplatePath),
                ArtifactName,
                (string readmeContent, (RepoInfo repo, Readme readme) context) => UpdateTagsListing(readmeContent, context.repo));

            ValidateArtifacts();
        }

        public (IReadOnlyDictionary<Value, Value> Symbols, string Indent) GetTemplateState(ManifestInfo manifest, string templatePath, string indent) =>
            GetCommonTemplateState(templatePath, manifest, (manifest, templatePath, currentIndent) => GetTemplateState(manifest, templatePath, currentIndent + indent), indent);

        public (IReadOnlyDictionary<Value, Value> Symbols, string Indent) GetTemplateState(RepoInfo repo, string templatePath, string indent)
        {
            (IReadOnlyDictionary<Value, Value> Symbols, string Indent) state = GetCommonTemplateState(
                templatePath,
                repo,
                (repo, templatePath, currentIndent) => GetTemplateState(repo, templatePath, currentIndent + indent),
                indent);
            Dictionary<Value, Value> symbols = new(state.Symbols);
            symbols["FULL_REPO"] = repo.QualifiedName;
            symbols["REPO"] = repo.Name;
            symbols["PARENT_REPO"] = GetParentRepoName(repo);
            symbols["SHORT_REPO"] = GetShortRepoName(repo);

            return (symbols, indent);
        }

        private (IReadOnlyDictionary<Value, Value> Symbols, string Indent) GetCommonTemplateState<TContext>(
            string sourceTemplatePath,
            TContext context,
            GetTemplateState<TContext> getState,
            string indent)
        {
            Dictionary<Value, Value> symbols = GetSymbols(sourceTemplatePath, context, getState, indent);
            symbols["IS_PRODUCT_FAMILY"] = context is ManifestInfo;

            return (symbols, indent);
        }

        private static string GetParentRepoName(RepoInfo repo)
        {
            string[] parts = repo.Name.Split('/');
            return parts.Length > 1 ? parts[parts.Length - 2] : string.Empty;
        }

        private static string GetShortRepoName(RepoInfo repo)
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
            string tagsListing = GenerateTagsListing(repo.Name, tagsMetadata);
            return ReadmeHelper.UpdateTagsListing(readme, tagsListing);
        }

        private string GenerateTagsListing(string repoName, string tagsMetadata)
        {
            Logger.WriteSubheading("GENERATING TAGS LISTING");

            string tagsDoc = GenerateTables(repoName, tagsMetadata);

            if (Options.IsVerbose)
            {
                Logger.WriteSubheading($"Tags Documentation:");
                Logger.WriteMessage(tagsDoc);
            }

            return tagsDoc;
        }

        private static string GenerateTables(string repoName, string tagsMetadata)
        {
            TagMetadataManifest metadataManifest = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<TagMetadataManifest>(tagsMetadata);

            // While the schema of the tag metadata supports multiple repos, we call this operation on a per-repo basis so only one repo output is expected
            RepoTagGroups repoTagGroups = metadataManifest.Repos.Single();

            return GenerateTables(repoTagGroups.TagGroups).Replace("\r\n", "\n");
        }

        private static string GenerateTables(IEnumerable<TagGroup> tagGroups)
        {
            StringBuilder tables = new();
            IEnumerable<IGrouping<(string OS, string Architecture, string? OsVersion), TagGroup>> tagGroupsGroupedByOsArch = tagGroups
                .GroupBy(tagGroup => (tagGroup.OS, tagGroup.Architecture, tagGroup.OS == "windows" ? tagGroup.OsVersion : null));
            bool isFirstTable = true;

            foreach (IGrouping<(string OS, string Architecture, string? OsVersion), TagGroup> groupedTagGroups in tagGroupsGroupedByOsArch)
            {
                if (isFirstTable)
                {
                    isFirstTable = false;
                }
                else
                {
                    tables.AppendLine();
                }

                string title;
                string tableHeader;
                if (groupedTagGroups.Key.OsVersion is not null)
                {
                    title = groupedTagGroups.Key.OsVersion;
                    tableHeader = WindowsTableHeader;
                }
                else
                {
                    title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(groupedTagGroups.Key.OS);
                    tableHeader = LinuxTableHeader;
                }

                AddOsTableContent(groupedTagGroups, title, groupedTagGroups.Key.Architecture, tableHeader, tables);
            }

            return tables.ToString();
        }

        private static void AddOsTableContent(IEnumerable<TagGroup> tagGroups, string title, string arch, string tableHeader, StringBuilder tables)
        {
            // Group tags by custom sub table title (e.g. Preview tags). Those tags without a custom sub table title will be listed first.
            List<IGrouping<string, TagGroup>> tagGroupGroupings = tagGroups
                .GroupBy(tg => tg.CustomSubTableTitle)
                .OrderBy(group => group.Key)
                .ToList();

            for (int i = 0; i < tagGroupGroupings.Count; i++)
            {
                if (i > 0)
                {
                    tables.AppendLine();
                }

                IGrouping<string, TagGroup> tagGroupGrouping = tagGroupGroupings[i];

                if (!string.IsNullOrEmpty(tagGroupGrouping.Key))
                {
                    tables.AppendLine("#### " + tagGroupGrouping.Key);
                    tables.AppendLine();
                    tables.AppendLine(tableHeader);
                }
                else
                {
                    tables.AppendLine($"### {title} {arch} Tags");
                    tables.AppendLine();
                    tables.AppendLine(tableHeader);
                }

                foreach (TagGroup tagGroup in tagGroupGrouping)
                {
                    tables.AppendLine(FormatTagGroupRow(tagGroup));
                }

            }
        }

        private static string FormatTagGroupRow(TagGroup tagGroup)
        {
            string row = $"{string.Join(", ", tagGroup.Tags)} | [Dockerfile]({tagGroup.Dockerfile})";
            if (tagGroup.OS != "windows")
            {
                row += $" | {tagGroup.OsVersion}";
            }

            return row;
        }

        private class TagMetadataManifest
        {
            public List<RepoTagGroups> Repos { get; set; } = [];
        }

        private class RepoTagGroups
        {
            public string RepoName { get; set; } = null!;

            public bool CustomTablePivots { get; set; }

            public List<TagGroup> TagGroups { get; set; } = [];
        }
    }
}
