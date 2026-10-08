// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Loads and parses manifest JSON files (manifest.json) into <see cref="ManifestInfo"/> view models.
/// Handles reading from disk, processing include files, validating the manifest model,
/// and constructing the full <see cref="ManifestInfo"/> object graph including repos, images, and platforms.
/// </summary>
/// <remarks>
/// This service is distinct from <see cref="IManifestService"/>, which queries Docker registries
/// for OCI/Docker image manifests. The naming overlap is due to "manifest" being an overloaded term:
/// this service deals with the repository's <c>manifest.json</c> build metadata files.
/// </remarks>
public class ManifestJsonService(IFileSystem fileSystem, ILogger<ManifestJsonService> logger) : IManifestJsonService
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ILogger<ManifestJsonService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public ManifestInfo Load(IManifestOptionsInfo options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation("READING MANIFEST");

        ManifestInfo manifest = Create(
            options.Manifest,
            options.GetManifestFilter(),
            options);

        if (options.IsVerbose)
        {
            _logger.LogInformation(JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }

        return manifest;
    }

    /// <summary>
    /// Creates a fully initialized <see cref="ManifestInfo"/> from the manifest file at
    /// <paramref name="manifestPath"/>, applying the given filter and options.
    /// </summary>
    private ManifestInfo Create(string manifestPath, ManifestFilter manifestFilter, IManifestOptionsInfo options)
    {
        string manifestDirectory = PathHelper.GetNormalizedDirectory(manifestPath);
        Manifest model = LoadModel(manifestPath, manifestDirectory);
        model.Validate(manifestDirectory);

        ManifestInfo manifestInfo = new()
        {
            Model = model,
            Registry = options.RegistryOverride ?? model.Registry,
            Directory = manifestDirectory
        };

        manifestInfo.VariableHelper = new VariableHelper(model, options, manifestInfo.GetRepoById);
        manifestInfo.AllRepos = manifestInfo.Model.Repos
            .Select(repo => RepoInfo.Create(
                repo,
                manifestInfo.Registry,
                model.Registry,
                manifestFilter,
                options,
                manifestInfo.VariableHelper,
                manifestInfo.Directory))
            .ToArray();

        if (model.Readme is not null)
        {
            manifestInfo.ReadmePath = Path.Combine(manifestInfo.Directory, model.Readme.Path);
            if (model.Readme.TemplatePath is not null)
            {
                manifestInfo.ReadmeTemplatePath = Path.Combine(manifestInfo.Directory, model.Readme.TemplatePath);
            }
        }

        IEnumerable<string> repoNames = manifestInfo.AllRepos.Select(repo => repo.QualifiedName).ToArray();
        foreach (PlatformInfo? platform in manifestInfo.GetAllPlatforms())
        {
            platform.Initialize(repoNames, manifestInfo.Registry);
        }

        IEnumerable<Repo> filteredRepoModels = manifestFilter.GetRepos(manifestInfo.Model);
        manifestInfo.FilteredRepos = manifestInfo.AllRepos
            .Where(repo => filteredRepoModels.Contains(repo.Model))
            .ToArray();

        return manifestInfo;
    }

    /// <summary>
    /// Reads the manifest JSON from disk, processes any include files, and returns the
    /// deserialized <see cref="Manifest"/> model with all includes merged.
    /// </summary>
    private Manifest LoadModel(string path, string manifestDirectory)
    {
        string manifestJson = _fileSystem.ReadAllText(path);
        ValidateTagSyndication(manifestJson, path);
        Manifest model =
            JsonConvert.DeserializeObject<Manifest>(manifestJson)
            ?? throw new InvalidOperationException($"Failed to deserialize manifest from '{path}'.");

        if (model.Includes is not null)
        {
            model.Variables ??= new Dictionary<string, string>();

            foreach (string? includePath in model.Includes)
            {
                ModelExtensions.ValidateFileReference(includePath, manifestDirectory);
                string fullIncludePath = Path.Combine(manifestDirectory, includePath);
                manifestJson = _fileSystem.ReadAllText(fullIncludePath);
                ValidateTagSyndication(manifestJson, fullIncludePath);
                Manifest includeModel =
                    JsonConvert.DeserializeObject<Manifest>(manifestJson)
                    ?? throw new InvalidOperationException(
                        $"Failed to deserialize included manifest from '{includePath}'.");

                foreach (KeyValuePair<string, string> kvp in includeModel.Variables)
                {
                    if (model.Variables.ContainsKey(kvp.Key))
                    {
                        throw new InvalidOperationException(
                            $"The manifest contains multiple '{kvp.Key}' variables."
                                + " Each variable name must be unique.");
                    }

                    model.Variables.Add(kvp.Key, kvp.Value);
                }

                model.Repos = [.. model.Repos, .. includeModel.Repos];

                // Consolidate distinct repo instances that share the same name
                model.Repos = model.Repos.GroupBy(repo => repo.Name).Select(ConsolidateReposWithSameName).ToArray();
            }
        }

        return model;
    }

    private static void ValidateTagSyndication(string manifestJson, string path)
    {
        using JsonDocument document = JsonDocument.Parse(
            manifestJson,
            new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

        IEnumerable<JsonElement> images = GetArrayProperty(document.RootElement, "repos")
            .SelectMany(repo => GetArrayProperty(repo, "images"));

        foreach (JsonElement image in images)
        {
            IEnumerable<JsonElement> tagSets = GetArrayProperty(image, "platforms")
                .Select(platform => GetProperty(platform, "tags"))
                .Prepend(GetProperty(image, "sharedTags"))
                .Where(tags => tags.ValueKind == JsonValueKind.Object);

            foreach (JsonElement tags in tagSets)
            {
                foreach (JsonProperty tag in tags.EnumerateObject())
                {
                    if (GetProperty(tag.Value, "syndication").ValueKind != JsonValueKind.Undefined)
                    {
                        throw new ValidationException(
                            $"Tag-level syndication on '{tag.Name}' in '{path}' is no longer supported. "
                                + "Set the image's 'syndication' repository string instead.");
                    }
                }
            }
        }

        static IEnumerable<JsonElement> GetArrayProperty(JsonElement element, string name)
        {
            JsonElement property = GetProperty(element, name);
            return property.ValueKind == JsonValueKind.Array ? property.EnumerateArray() : [];
        }

        static JsonElement GetProperty(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object
                ? element
                    .EnumerateObject()
                    .LastOrDefault(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    .Value
                : default;
    }

    /// <summary>
    /// Merges multiple <see cref="Repo"/> instances that share the same name into a single
    /// consolidated repo, combining their images and validating that non-image properties
    /// do not conflict.
    /// </summary>
    private static Repo ConsolidateReposWithSameName(IGrouping<string, Repo> grouping)
    {
        // Validate that all repos which share the same name also don't have
        // non-empty, conflicting values for the other settings
        IEnumerable<PropertyInfo> propertiesToValidate = typeof(Repo)
            .GetProperties()
            .Where(prop => prop.Name != nameof(Repo.Images) && prop.Name != nameof(Repo.Readmes));

        foreach (PropertyInfo property in propertiesToValidate)
        {
            List<string> distinctNonEmptyPropertyValues = grouping
                .Select(property.GetValue)
                .Distinct()
                .Select(item => item?.ToString())
                .Where(val => !string.IsNullOrEmpty(val))
                .Select(val => $"'{val}'")
                .ToList();

            if (distinctNonEmptyPropertyValues.Count > 1)
            {
                throw new InvalidOperationException(
                    "The manifest contains multiple repos with the same name that also do not have the same "
                        + $"value for the '{property.Name}' property. Distinct values: "
                        + string.Join(", ", distinctNonEmptyPropertyValues));
            }
        }

        // Create a new consolidated repo model instance that contains whichever non-empty state
        // was set amongst the group of repos. All of the images within the repos are combined
        // together into a single set.
        return new Repo
        {
            Name = grouping.Key,
            Id = grouping
                .Select(repo => repo.Id)
                .FirstOrDefault(val => !string.IsNullOrEmpty(val)),
            McrTagsMetadataTemplate = grouping
                .Select(repo => repo.McrTagsMetadataTemplate)
                .FirstOrDefault(val => !string.IsNullOrEmpty(val)),
            Readmes = [.. grouping.SelectMany(repo => repo.Readmes)],
            Images = [.. grouping.SelectMany(repo => repo.Images)],
        };
    }
}
