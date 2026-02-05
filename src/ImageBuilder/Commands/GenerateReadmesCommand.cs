#nullable disable
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

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public partial class GenerateReadmesCommand(
        IManifestInfoProvider manifestInfoProvider,
        IEnvironmentService environmentService)
        : GenerateArtifactsCommand<GenerateReadmesOptions, GenerateReadmesOptionsBuilder>(manifestInfoProvider, environmentService)
    {
        private const string ArtifactName = "Readme";

        protected override string Description =>
            "Generates the Readmes from the Cottle based templates (http://r3c.github.io/cottle/) and updates the tag listing section";

        /// <summary>
        /// Orchestrates readme generation for both the product family and individual repos,
        /// then validates all generated artifacts.
        /// </summary>
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

        /// <summary>
        /// Builds template state for product family readmes, recursively resolving nested template includes.
        /// </summary>
        /// <param name="templatePath">Must be a valid path to an existing Cottle template file.</param>
        /// <param name="indent">Accumulated indentation from parent templates; empty string for root calls.</param>
        public (IReadOnlyDictionary<Value, Value> Symbols, string Indent) GetTemplateState(ManifestInfo manifest, string templatePath, string indent) =>
            GetCommonTemplateState(templatePath, manifest, (manifest, templatePath, currentIndent) => GetTemplateState(manifest, templatePath, currentIndent + indent), indent);

        /// <summary>
        /// Builds template state for repo-specific readmes, adding repo-specific symbols like FULL_REPO and SHORT_REPO.
        /// </summary>
        /// <param name="templatePath">Must be a valid path to an existing Cottle template file.</param>
        /// <param name="indent">Accumulated indentation from parent templates; empty string for root calls.</param>
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

        /// <summary>
        /// Creates the base template state shared by both manifest and repo templates,
        /// including the IS_PRODUCT_FAMILY flag used for conditional template logic.
        /// </summary>
        /// <typeparam name="TContext">Either ManifestInfo or RepoInfo depending on the readme scope.</typeparam>
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

        /// <summary>
        /// Replaces the tags listing section in a readme with freshly generated tag tables.
        /// No-op if the repo lacks MCR tags metadata configuration.
        /// </summary>
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

        /// <summary>
        /// Extracts tag groups from MCR tags metadata YAML. Each tag group represents
        /// a set of tags pointing to the same image digest.
        /// </summary>
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

        /// <summary>
        /// Converts tag groups into formatted markdown tables, organized by OS/architecture sections.
        /// </summary>
        private static string GenerateMarkdownTables(IEnumerable<TagGroup> tagGroups)
        {
            StringBuilder tables = new();
            const int HeadingLevel = 3;

            var sections = GroupTagsIntoSections(tagGroups).ToList();
            sections.ForEach(tableSection => tableSection.RenderAsMarkdown(tables, HeadingLevel));

            return tables.ToString();
        }

        /// <summary>
        /// Represents a section of the tags listing, containing one top level
        /// table and zero or more nested sub-tables.
        /// </summary>
        /// <param name="Title">The section title</param>
        /// <param name="Tags">The tag groups to display in this section's table</param>
        /// <param name="SubTables">Nested sub-sections</param>
        private sealed record TagsTable(
            string Title,
            IEnumerable<TagGroup> Tags,
            IEnumerable<TagsTable> SubTables)
        {
            /// <summary>
            /// Creates a leaf section with no nested sub-tables.
            /// </summary>
            public TagsTable(string title, IEnumerable<TagGroup> tags)
                : this(title, tags, []) { }

            /// <summary>
            /// Appends this section and all nested sub-tables to the builder,
            /// incrementing heading depth for each nesting level.
            /// </summary>
            /// <param name="headingLevel">
            /// Must be between 1-6 per markdown spec; exceeding 6 renders as plain text.
            /// </param>
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
        /// <summary>
        /// Extracts the parent segment from a hierarchical repo name (e.g., "dotnet" from "dotnet/runtime").
        /// Returns empty string for flat repo names without hierarchy.
        /// </summary>
        public static string GetParentRepoName(this RepoInfo repo)
        {
            string[] parts = repo.Name.Split('/');
            return parts.Length > 1 ? parts[parts.Length - 2] : string.Empty;
        }

        /// <summary>
        /// Extracts the final segment from a hierarchical repo name (e.g., "runtime" from "dotnet/runtime").
        /// Returns the full name if no hierarchy exists.
        /// </summary>
        public static string GetShortName(this RepoInfo repo)
        {
            int lastSlashIndex = repo.Name.LastIndexOf('/');
            return lastSlashIndex == -1 ? repo.Name : repo.Name.Substring(lastSlashIndex + 1);
        }
    }
}
