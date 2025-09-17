// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using static Microsoft.DotNet.ImageBuilder.ReadModel.JsonHelper;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

public static class ManifestInfoExtensions
{
    extension(ManifestInfo manifestInfo)
    {
        public static async Task<ManifestInfo> LoadAsync(string manifestJsonPath)
        {
            var manifestJsonObject = await LoadModelFromFileAsync(manifestJsonPath);
            var manifestDir = Path.GetDirectoryName(manifestJsonPath) ?? "";

            // Load and deserialize included files
            IEnumerable<JsonObject> includesJsonNodes = [];
            var includesNode = manifestJsonObject["includes"];
            if (includesNode is not null)
            {
                IEnumerable<string> includesFiles = Deserialize<string[]>(includesNode);
                includesJsonNodes = await Task.WhenAll(
                    includesFiles
                        // Make includes paths relative to the manifest file
                        .Select(includesFile => Path.Combine(manifestDir, includesFile))
                        .Select(LoadModelFromFileAsync));
            }

            var preprocessor = new ManifestPreprocessor();
            var processedRootJsonNode = preprocessor.Process(manifestJsonObject, includesJsonNodes);
            var processedModel = Deserialize<Manifest>(processedRootJsonNode);

            return ManifestInfo.Create(processedModel);
        }

        public string ToJsonString() => Serialize(manifestInfo.Model);

        internal static ManifestInfo Create(Manifest model)
        {
            var repoInfos = model.Repos
                .Select(repo => RepoInfo.Create(repo, model))
                .ToImmutableList();

            return new ManifestInfo(model, repoInfos);
        }
    }

    private static async Task<JsonObject> LoadModelFromFileAsync(string manifestJsonPath)
    {
        var jsonStream = File.OpenRead(manifestJsonPath);
        var rootJsonNode = await JsonNode.ParseAsync(jsonStream)
            ?? throw new Exception(
                $"Failed to parse manifest JSON from file: {manifestJsonPath}");

        if (rootJsonNode is not JsonObject rootJsonObject)
        {
            throw new InvalidDataException($"Manifest root must be a JSON object.");
        }

        return rootJsonObject;
    }
}
