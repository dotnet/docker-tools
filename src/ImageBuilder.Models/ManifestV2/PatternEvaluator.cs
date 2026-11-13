// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

internal sealed partial class PatternEvaluator
{
    private readonly string _scopeKey;

    // Everything a placeholder can refer to: the inherited metadata and the built-in model values.
    private readonly Dictionary<string, JsonElement> _variables;

    // Paths to built-in values that differ between the platforms in scope, like "arch" for an image.
    private readonly HashSet<string> _platformSpecificPaths = new(StringComparer.Ordinal);

    // Placeholders being expanded, to reject metadata values that refer to themselves.
    private readonly HashSet<string> _activePlaceholders = new(StringComparer.Ordinal);

    public PatternEvaluator(string repoKey, string scopeKey, Dictionary<string, JsonElement> metadata, Platform[] platforms)
    {
        _scopeKey = scopeKey;
        _variables = new(metadata, StringComparer.Ordinal);
        if (platforms.Length == 0)
        {
            return;
        }

        List<JsonElement> builtIns = [];
        foreach (Platform platform in platforms)
        {
            JsonElement builtIn = BuiltInVariables.Serialize(repoKey, platform);
            builtIns.Add(builtIn);
        }

        JsonElement firstBuiltIns = builtIns[0];
        foreach (JsonProperty builtIn in firstBuiltIns.EnumerateObject())
        {
            if (metadata.ContainsKey(builtIn.Name))
            {
                throw new InvalidDataException(
                    $"{_scopeKey}: metadata key '{builtIn.Name}' is reserved for a built-in placeholder.");
            }
        }

        Dictionary<string, JsonElement> commonBuiltIns = GetCommonProperties(builtIns, pathPrefix: "");
        foreach ((string name, JsonElement value) in commonBuiltIns)
        {
            _variables.Add(name, value);
        }
    }

    public string Expand(string pattern)
    {
        string expanded = PlaceholderRegex.Replace(pattern, ReplacePlaceholder);
        if (expanded.Contains('{') || expanded.Contains('}'))
        {
            throw new InvalidDataException($"{_scopeKey}: pattern '{pattern}' contains malformed placeholders.");
        }

        return expanded;
    }

    private string ReplacePlaceholder(Match match)
    {
        string placeholder = match.Groups[1].Value;
        return ResolvePlaceholder(placeholder);
    }

    public Dictionary<string, JsonElement> ExpandMetadata(Dictionary<string, JsonElement> metadata)
    {
        Dictionary<string, JsonElement> expanded = new(StringComparer.Ordinal);
        foreach ((string key, JsonElement value) in metadata)
        {
            expanded[key] = ExpandValue(value);
        }

        return expanded;
    }

    private JsonElement ExpandValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => ExpandString(value),
        JsonValueKind.Object => ExpandObject(value),
        JsonValueKind.Array => ExpandArray(value),
        _ => value
    };

    private JsonElement ExpandString(JsonElement value)
    {
        string pattern = value.ToString();
        string expanded = Expand(pattern);
        return JsonSerializer.SerializeToElement(expanded);
    }

    private JsonElement ExpandObject(JsonElement value)
    {
        Dictionary<string, JsonElement> expanded = [];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            expanded[property.Name] = ExpandValue(property.Value);
        }

        return JsonSerializer.SerializeToElement(expanded);
    }

    private JsonElement ExpandArray(JsonElement value)
    {
        List<JsonElement> expanded = [];
        foreach (JsonElement item in value.EnumerateArray())
        {
            expanded.Add(ExpandValue(item));
        }

        return JsonSerializer.SerializeToElement(expanded);
    }

    public Dictionary<string, Tag> ResolveTags(Dictionary<string, Tag> tags)
    {
        Dictionary<string, Tag> resolvedTags = new(StringComparer.Ordinal);
        foreach ((string pattern, Tag tag) in tags)
        {
            if (tag is null)
            {
                throw new InvalidDataException(
                    $"{_scopeKey}: tag '{pattern}' must have an options mapping; use '{{}}' for default options.");
            }

            string name = ExpandTagName(pattern);
            if (!resolvedTags.TryAdd(name, tag))
            {
                throw new InvalidDataException($"{_scopeKey}: multiple tag declarations resolve to '{name}'.");
            }
        }

        return resolvedTags;
    }

    private string ExpandTagName(string pattern)
    {
        string name = Expand(pattern);
        if (!DockerTagRegex.IsMatch(name))
        {
            throw new InvalidDataException($"{_scopeKey}: resolved tag '{name}' is not a valid Docker tag.");
        }

        return name;
    }

    private string ResolvePlaceholder(string placeholder)
    {
        string[] segments = placeholder.Split('.');
        if (!_variables.TryGetValue(segments[0], out JsonElement value))
        {
            throw UnresolvedPlaceholder(placeholder, segments[0]);
        }

        for (int i = 1; i < segments.Length; i++)
        {
            bool found = value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segments[i], out value);
            if (!found)
            {
                string missingPath = string.Join('.', segments[..(i + 1)]);
                throw UnresolvedPlaceholder(placeholder, missingPath);
            }
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => ExpandNested(placeholder, value.ToString()),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => throw new InvalidDataException($"{_scopeKey}: pattern variable '{placeholder}' must be a non-null scalar.")
        };
    }

    private InvalidDataException UnresolvedPlaceholder(string placeholder, string missingPath)
    {
        if (_platformSpecificPaths.Contains(missingPath))
        {
            return new InvalidDataException($"{_scopeKey}: pattern variable '{placeholder}' is ambiguous across platforms.");
        }

        return new InvalidDataException($"{_scopeKey}: unknown pattern variable '{placeholder}'.");
    }

    // A metadata value may itself contain placeholders, such as "{monitorUrl}.sha512".
    private string ExpandNested(string placeholder, string value)
    {
        if (!_activePlaceholders.Add(placeholder))
        {
            throw new InvalidDataException($"{_scopeKey}: pattern variable '{placeholder}' refers to itself.");
        }

        try
        {
            return Expand(value);
        }
        finally
        {
            _activePlaceholders.Remove(placeholder);
        }
    }

    // The properties that all of the objects agree on. Objects are compared property by property, so an
    // image whose platforms share an OS but not an arch can still use {os.name}, but not {arch}.
    private Dictionary<string, JsonElement> GetCommonProperties(List<JsonElement> objects, string pathPrefix)
    {
        Dictionary<string, JsonElement> common = new(StringComparer.Ordinal);
        JsonElement firstObject = objects[0];
        foreach (JsonProperty property in firstObject.EnumerateObject())
        {
            string path = pathPrefix + property.Name;
            List<JsonElement> values = [];
            foreach (JsonElement obj in objects)
            {
                values.Add(obj.GetProperty(property.Name));
            }

            JsonElement firstValue = values[0];
            if (values.All(value => value.ValueKind == JsonValueKind.Object))
            {
                Dictionary<string, JsonElement> commonChildren = GetCommonProperties(values, path + ".");
                JsonElement commonObject = JsonSerializer.SerializeToElement(commonChildren);
                common.Add(property.Name, commonObject);
            }
            else if (values.All(value => JsonElement.DeepEquals(value, firstValue)))
            {
                common.Add(property.Name, firstValue);
            }
            else
            {
                _platformSpecificPaths.Add(path);
            }
        }

        return common;
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex PlaceholderRegex { get; }

    [GeneratedRegex(@"\A[a-zA-Z0-9_][a-zA-Z0-9_.-]{0,127}\z")]
    private static partial Regex DockerTagRegex { get; }
}
