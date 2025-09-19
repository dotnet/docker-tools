// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ReadModel.Serialization;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

internal sealed class ManifestPreprocessor
{
    private IVariableStore _variableStore = new EmptyVariableStore();

    public JsonNode Process(JsonObject root, IEnumerable<JsonObject> includesNodes)
    {
        // Process includes first so variables in included files can be processed
        ProcessIncludes(root, includesNodes);

        var rawManifest = JsonHelper.Deserialize(root, ManifestSerializationContext.Default.Manifest);
        var variables = rawManifest.Variables ?? new Dictionary<string, string>();

        // Add variables for each repo name (e.g. "Repo:dotnet" -> "mcr.microsoft.com/dotnet")
        foreach (var kvp in rawManifest.RepoVariables)
        {
            variables.Add(kvp);
        }

        _variableStore = new VariableStore(variables);

        ProcessVariables(root);
        return root;
    }

    /// <summary>
    /// Replace keys and string values in JSON that reference variables defined
    /// in the variable store.
    /// </summary>
    private void ProcessVariables(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                var jsonObjectSnapshot = jsonObject.ToList();
                foreach ((string oldKey, JsonNode? value) in jsonObjectSnapshot)
                {
                    var newKey = _variableStore.ResolveInnerVariables(oldKey);
                    if (newKey != oldKey)
                    {
                        jsonObject.Remove(oldKey);
                        jsonObject[newKey] = value;
                    }

                    ProcessVariables(value);
                }
                break;

            case JsonArray jsonArray:
                for (int i = 0; i < jsonArray.Count; i++)
                {
                    ProcessVariables(jsonArray[i]);
                }
                break;

            case JsonValue jsonValue:
                if (jsonValue.TryGetValue(out string? stringValue))
                {
                    jsonValue.ReplaceWith(_variableStore.ResolveInnerVariables(stringValue));
                }
                break;

            case null:
                break;
        }
    }

    private static void ProcessIncludes(JsonObject jsonObject, IEnumerable<JsonObject> includes)
    {
        foreach (JsonObject includeObject in includes)
        {
            jsonObject.Merge(includeObject);
        }
    }
}

internal static class ManifestRepoVariableExtensions
{
    extension(Manifest manifest)
    {
        public IEnumerable<KeyValuePair<string, string>> RepoVariables =>
            manifest.Repos
                .Where(repo => !string.IsNullOrWhiteSpace(repo.Id))
                .Select(repo => new KeyValuePair<string, string>($"Repo:{repo.Id}", repo.Name));
    }
}
