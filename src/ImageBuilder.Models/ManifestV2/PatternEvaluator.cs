// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

internal sealed partial class PatternEvaluator(
    string repoKey,
    string scopeKey,
    Dictionary<string, JsonElement> metadata,
    Platform[] platforms)
{
    // Placeholders being expanded, to reject metadata values that refer to themselves.
    private readonly HashSet<string> _activePlaceholders = new(StringComparer.Ordinal);

    public string Expand(string pattern)
    {
        string expanded = PlaceholderRegex.Replace(pattern, match => ResolvePlaceholder(match.Groups[1].Value));
        if (expanded.Contains('{') || expanded.Contains('}'))
        {
            throw new InvalidDataException($"{scopeKey}: pattern '{pattern}' contains malformed placeholders.");
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
                    $"{scopeKey}: tag '{pattern}' must have an options mapping; use '{{}}' for default options.");
            }

            string name = ExpandTagName(pattern);
            if (!resolvedTags.TryAdd(name, tag))
            {
                throw new InvalidDataException($"{scopeKey}: multiple tag declarations resolve to '{name}'.");
            }
        }

        return resolvedTags;
    }

    private string ExpandTagName(string pattern)
    {
        string name = Expand(pattern);
        if (!DockerTagRegex.IsMatch(name))
        {
            throw new InvalidDataException($"{scopeKey}: resolved tag '{name}' is not a valid Docker tag.");
        }

        return name;
    }

    private string ResolvePlaceholder(string placeholder)
    {
        string? modelValue = placeholder switch
        {
            "repoKey" => repoKey,
            "os.family" => ResolvePlatformValue(
                placeholder, platform => platform.OS.Family.ToString().ToLowerInvariant()),
            "os.name" => ResolvePlatformValue(placeholder, platform => platform.OS.Name),
            "arch" => ResolvePlatformValue(
                placeholder, platform => JsonNamingPolicy.CamelCase.ConvertName(platform.Arch.ToString())),
            _ => null
        };
        if (modelValue is not null)
        {
            return modelValue;
        }

        string[] segments = placeholder.Split('.');
        if (!metadata.TryGetValue(segments[0], out JsonElement value))
        {
            throw new InvalidDataException($"{scopeKey}: unknown pattern variable '{placeholder}'.");
        }

        foreach (string segment in segments.Skip(1))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                throw new InvalidDataException($"{scopeKey}: unknown pattern variable '{placeholder}'.");
            }
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => ExpandNested(placeholder, value.GetString()!),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => throw new InvalidDataException($"{scopeKey}: pattern variable '{placeholder}' must be a non-null scalar.")
        };
    }

    // A metadata value may itself contain placeholders, such as "{monitorUrl}.sha512".
    private string ExpandNested(string placeholder, string value)
    {
        if (!_activePlaceholders.Add(placeholder))
        {
            throw new InvalidDataException($"{scopeKey}: pattern variable '{placeholder}' refers to itself.");
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

    private string? ResolvePlatformValue(string placeholder, Func<Platform, string> selector)
    {
        string[] values = platforms.Select(selector).Distinct(StringComparer.Ordinal).Take(2).ToArray();
        return values.Length switch
        {
            0 => null,
            1 => values[0],
            _ => throw new InvalidDataException(
                $"{scopeKey}: pattern variable '{placeholder}' is ambiguous across platforms.")
        };
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex PlaceholderRegex { get; }

    [GeneratedRegex(@"\A[a-zA-Z0-9_][a-zA-Z0-9_.-]{0,127}\z")]
    private static partial Regex DockerTagRegex { get; }
}
