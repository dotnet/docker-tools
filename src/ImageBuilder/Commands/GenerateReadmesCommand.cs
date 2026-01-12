// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.ImageBuilder.Mcr;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.McrTags;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public partial class GenerateReadmesCommand(IEnvironmentService environmentService)
        : GenerateArtifactsCommand<GenerateReadmesOptions, GenerateReadmesOptionsBuilder>(environmentService)
    {
        private const string ArtifactName = "Readme";

        protected override string Description =>
            "Generates the Readmes from the Cottle based templates (http://r3c.github.io/cottle/) and updates the tag listing section";

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING READMES");

            // Generate Product Family Readme
            await GenerateArtifactsAsync(
                contexts: new ManifestInfo[] { Manifest },
                getTemplatePath: (manifest) => manifest.ReadmeTemplatePath,
                getArtifactPath: (manifest) => manifest.ReadmePath,
                getState: (manifest, templatePath, indent) => GetTemplateState(manifest, templatePath, indent),
                templatePropertyName: nameof(Readme.TemplatePath),
                artifactName: ArtifactName);

            // Generate Repo Readmes
            await GenerateArtifactsAsync(
                contexts: Manifest.FilteredRepos
                    .Select(repo => repo.Readmes.Select(readme => (repo, readme)))
                    .SelectMany(repoReadme => repoReadme),
                getTemplatePath: ((RepoInfo repo, Readme readme) context) => context.readme.TemplatePath,
                getArtifactPath: ((RepoInfo repo, Readme readme) context) => context.readme.Path,
                getState: ((RepoInfo repo, Readme readme) context, string templatePath, string indent) =>
                    GetTemplateState(context.repo, templatePath, indent),
                templatePropertyName: nameof(Readme.TemplatePath),
                artifactName: ArtifactName,
                postProcess: (string readmeContent, (RepoInfo repo, Readme readme) context) =>
                    UpdateTagsListing(readmeContent, context.repo));

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
            symbols["PARENT_REPO"] = repo.GetParentRepoName();
            symbols["SHORT_REPO"] = repo.GetShortName();

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

        private string UpdateTagsListing(string readme, RepoInfo repo)
        {
            if (repo.Model.McrTagsMetadataTemplate is null)
            {
                return readme;
            }

            var repoTagGroups = GenerateRepoTagGroups(repo);

            Logger.WriteSubheading("GENERATING TAGS LISTING");
            string tagsMarkdown = GenerateMarkdownTables(repoTagGroups);

            if (Options.IsVerbose)
            {
                Logger.WriteSubheading($"Tags Documentation:");
                Logger.WriteMessage(tagsMarkdown);
            }

            var updatedReadme = ReadmeHelper.UpdateTagsListing(readme, tagsMarkdown);
            return updatedReadme;
        }

        private IEnumerable<TagGroup> GenerateRepoTagGroups(RepoInfo repo)
        {
            var tagsMetadataYaml = McrTagsMetadataGenerator.Execute(Manifest, repo);
            var tagsMetadata = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<TagsMetadata>(tagsMetadataYaml);

            // While the schema of the tag metadata supports multiple repos, we call this operation
            // on a per-repo basis so only one repo output is expected. We also don't need the repo
            // metadata for generating tags tables either, so return only the tags.
            // Each tag group here is one set of tags that all point to the same image.
            var repoTagsMetadata = tagsMetadata.Repos.Single();
            return repoTagsMetadata.TagGroups;
        }

        /// <summary>
        /// Groups tag groups into table sections based on OS and architecture.
        /// Each section represents a logical grouping that will become one or more markdown tables.
        /// </summary>
        private static IEnumerable<TagsTable> GroupTagsIntoSections(IEnumerable<TagGroup> tagGroups)
        {
            var groupedByOsArch = tagGroups.GroupBy(tagGroup => (tagGroup.OS, tagGroup.Architecture));

            foreach (var osArchGroup in groupedByOsArch)
            {
                string title = $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(osArchGroup.Key.OS)} {osArchGroup.Key.Architecture} Tags";

                // Further group by custom sub-table title (e.g., "Preview tags")
                // Tags without a custom sub-table title come first (null/empty keys sort first)
                var subGroups = osArchGroup
                    .GroupBy(tagGroup => tagGroup.CustomSubTableTitle)
                    .OrderBy(g => g.Key)
                    .ToList();

                // If there's only one group with no custom title, no need for sub-sections
                if (subGroups.Count == 1 && string.IsNullOrEmpty(subGroups[0].Key))
                {
                    yield return new TagsTable(title, subGroups[0]);
                }
                else
                {
                    // Tags without custom title go directly in the parent section
                    // Tags with custom titles become sub-sections
                    var mainTags = subGroups
                        .Where(g => string.IsNullOrEmpty(g.Key))
                        .SelectMany(g => g);
                    var subSections = subGroups
                        .Where(g => !string.IsNullOrEmpty(g.Key))
                        .Select(subGroup => new TagsTable(subGroup.Key!, tags: subGroup));

                    yield return new TagsTable(title, mainTags, subSections);
                }
            }
        }

        private static string GenerateMarkdownTables(IEnumerable<TagGroup> tagGroups)
        {
            StringBuilder tables = new();
            const int HeadingLevel = 3;

            var sections = GroupTagsIntoSections(tagGroups).ToList();
            sections.ForEach(tableSection => tableSection.RenderAsMarkdown(tables, HeadingLevel));

            return tables.ToString();
        }

        /// <summary>
        /// Represents a section of the tags listing, containing one or more markdown tables.
        /// </summary>
        /// <param name="Title">The section title</param>
        /// <param name="Tags">The tag groups to display in this section's table</param>
        /// <param name="SubTables">Nested sub-sections</param>
        private sealed record TagsTable(
            string Title,
            IEnumerable<TagGroup> Tags,
            IEnumerable<TagsTable> SubTables)
        {
            public TagsTable(string title, IEnumerable<TagGroup> tags)
                : this(title, tags, []) { }

            public void RenderAsMarkdown(StringBuilder builder, int headingLevel = 3)
            {
                string headingPrefix = new('#', headingLevel);
                builder.AppendLine($"{headingPrefix} {Title}");
                builder.AppendLine();

                if (Tags.Any())
                {
                    builder.AppendLine(
                        """
                        Tags | Dockerfile | OS Version
                        ---- | ---------- | ----------
                        """);

                    foreach (TagGroup tagGroup in Tags)
                    {
                        var tagsString = string.Join(", ", tagGroup.Tags);
                        builder.AppendLine($"{tagsString} | [Dockerfile]({tagGroup.Dockerfile}) | {tagGroup.OsVersion}");
                    }
                }

                builder.AppendLine();

                foreach (var subTable in SubTables)
                {
                    subTable.RenderAsMarkdown(builder, headingLevel + 1);
                }
            }
        }
    }

    internal static class ReadmeTemplateVariablesExtensions
    {
        public static string GetParentRepoName(this RepoInfo repo)
        {
            string[] parts = repo.Name.Split('/');
            return parts.Length > 1 ? parts[parts.Length - 2] : string.Empty;
        }

        public static string GetShortName(this RepoInfo repo)
        {
            int lastSlashIndex = repo.Name.LastIndexOf('/');
            return lastSlashIndex == -1 ? repo.Name : repo.Name.Substring(lastSlashIndex + 1);
        }
    }
}
