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

        JsonElement[] builtIns = [.. platforms.Select(platform => BuiltInVariables.Serialize(repoKey, platform))];
        if (builtIns.Length == 0)
        {
            return;
        }

        foreach (JsonProperty builtIn in builtIns[0].EnumerateObject())
        {
            if (metadata.ContainsKey(builtIn.Name))
            {
                throw new InvalidDataException(
                    $"{_scopeKey}: metadata key '{builtIn.Name}' is reserved for a built-in placeholder.");
            }
        }

        foreach ((string name, JsonElement value) in GetCommonProperties(builtIns, pathPrefix: ""))
        {
            _variables.Add(name, value);
        }
    }

    public string Expand(string pattern)
    {
        string expanded = PlaceholderRegex.Replace(pattern, match => ResolvePlaceholder(match.Groups[1].Value));
        if (expanded.Contains('{') || expanded.Contains('}'))
        {
            throw new InvalidDataException($"{_scopeKey}: pattern '{pattern}' contains malformed placeholders.");
        }

        return expanded;
    }

    public Dictionary<string, JsonElement> ExpandMetadata(Dictionary<string, JsonElement> metadata) =>
        metadata.ToDictionary(entry => entry.Key, entry => ExpandValue(entry.Value), StringComparer.Ordinal);

    private JsonElement ExpandValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => JsonSerializer.SerializeToElement(Expand(value.GetString()!)),
        JsonValueKind.Object => JsonSerializer.SerializeToElement(
            value.EnumerateObject().ToDictionary(property => property.Name, property => ExpandValue(property.Value))),
        JsonValueKind.Array => JsonSerializer.SerializeToElement(value.EnumerateArray().Select(ExpandValue).ToArray()),
        _ => value
    };

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
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segments[i], out value))
            {
                throw UnresolvedPlaceholder(placeholder, string.Join('.', segments[..(i + 1)]));
            }
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => ExpandNested(placeholder, value.GetString()!),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => throw new InvalidDataException($"{_scopeKey}: pattern variable '{placeholder}' must be a non-null scalar.")
        };
    }

    private InvalidDataException UnresolvedPlaceholder(string placeholder, string missingPath) =>
        _platformSpecificPaths.Contains(missingPath)
            ? new InvalidDataException($"{_scopeKey}: pattern variable '{placeholder}' is ambiguous across platforms.")
            : new InvalidDataException($"{_scopeKey}: unknown pattern variable '{placeholder}'.");

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
    private Dictionary<string, JsonElement> GetCommonProperties(JsonElement[] objects, string pathPrefix)
    {
        Dictionary<string, JsonElement> common = new(StringComparer.Ordinal);
        foreach (JsonProperty property in objects[0].EnumerateObject())
        {
            string path = pathPrefix + property.Name;
            JsonElement[] values = [.. objects.Select(obj => obj.GetProperty(property.Name))];
            if (values.All(value => value.ValueKind == JsonValueKind.Object))
            {
                common.Add(property.Name, JsonSerializer.SerializeToElement(GetCommonProperties(values, path + ".")));
            }
            else if (values.All(value => JsonElement.DeepEquals(value, values[0])))
            {
                common.Add(property.Name, values[0]);
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
