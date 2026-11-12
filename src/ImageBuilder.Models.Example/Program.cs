// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:project ..\ImageBuilder.Models\Microsoft.DotNet.ImageBuilder.Models.csproj
#:package YamlDotNet
#:property PublishAot=false

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using Group = Microsoft.DotNet.ImageBuilder.Models.ManifestV2.Group;

// Run from this directory: dotnet run --file Program.cs
if (args.Length > 1)
{
    throw new ArgumentException("Expected at most one argument: the manifest path.");
}

string inputPath = args.Length == 1 ? args[0] : "manifest.yaml";
string outputPath = Path.ChangeExtension(inputPath, ".resolved.json");

Manifest manifest = await ManifestYaml.ReadAsync(inputPath);
Manifest resolved = new ManifestResolver(manifest).Resolve();
await ManifestJson.WriteAsync(outputPath, resolved);
Console.WriteLine($"Wrote {outputPath}");

file sealed class ManifestResolver(Manifest manifest)
{
    public Manifest Resolve()
    {
        Dictionary<string, Repo> repos = manifest.Repos.ToDictionary(
            repoEntry => repoEntry.Key,
            repoEntry => ResolveRepo(repoEntry.Key, repoEntry.Value, manifest.Metadata, manifest.Defaults));

        return manifest with { Repos = repos, Defaults = null };
    }

    private static Repo ResolveRepo(
        string repoKey, Repo repo, Dictionary<string, JsonElement> parentMetadata, ManifestDefaults? parentDefaults)
    {
        Dictionary<string, JsonElement> metadata = InheritMetadata(parentMetadata, repo.Metadata);
        ManifestDefaults defaults = InheritDefaults(parentDefaults, repo.Defaults);
        Dictionary<string, Image> images = ResolveImages(repoKey, repoKey, repo.Images, metadata, defaults);
        Dictionary<string, Group> groups = repo.Groups.ToDictionary(
            groupEntry => groupEntry.Key,
            groupEntry => ResolveGroup(
                repoKey, $"{repoKey}__{groupEntry.Key}", groupEntry.Value, metadata, defaults));

        ValidateUniqueTags(
            repoKey,
            [
                (repoKey, images),
                .. groups.Select(groupEntry => ($"{repoKey}__{groupEntry.Key}", groupEntry.Value.Images))
            ]);
        return repo with { Images = images, Groups = groups, Metadata = metadata, Defaults = null };
    }

    private static Group ResolveGroup(
        string repoKey, string scopeKey, Group group,
        Dictionary<string, JsonElement> parentMetadata, ManifestDefaults parentDefaults)
    {
        Dictionary<string, JsonElement> metadata = InheritMetadata(parentMetadata, group.Metadata);
        ManifestDefaults defaults = InheritDefaults(parentDefaults, group.Defaults);
        Dictionary<string, Image> images = ResolveImages(repoKey, scopeKey, group.Images, metadata, defaults);
        return group with { Images = images, Metadata = metadata, Defaults = null };
    }

    private static Dictionary<string, Image> ResolveImages(
        string repoKey, string scopeKey, Dictionary<string, Image> images,
        Dictionary<string, JsonElement> parentMetadata, ManifestDefaults parentDefaults) =>
        images.ToDictionary(
            imageEntry => imageEntry.Key,
            imageEntry => ResolveImage(
                repoKey, $"{scopeKey}__{imageEntry.Key}", imageEntry.Value, parentMetadata, parentDefaults));

    private static Image ResolveImage(
        string repoKey, string scopeKey, Image image,
        Dictionary<string, JsonElement> parentMetadata, ManifestDefaults parentDefaults)
    {
        Dictionary<string, JsonElement> metadata = InheritMetadata(parentMetadata, image.Metadata);
        ManifestDefaults defaults = InheritDefaults(parentDefaults, image.Defaults);
        PatternEvaluator evaluator = new(repoKey, scopeKey, metadata, [.. image.Platforms.Values]);
        Dictionary<string, Tag> tags = evaluator.ResolveTags(image.Tags ?? defaults.Image.Tags ?? []);
        Dictionary<string, Platform> platforms = image.Platforms.ToDictionary(
            platformEntry => platformEntry.Key,
            platformEntry => ResolvePlatform(
                repoKey, $"{scopeKey}__{platformEntry.Key}", platformEntry.Value, metadata, defaults.Platform));

        return image with
        {
            Tags = tags,
            Platforms = platforms,
            Metadata = metadata,
            Defaults = null
        };
    }

    private static Platform ResolvePlatform(
        string repoKey, string scopeKey, Platform platform,
        Dictionary<string, JsonElement> parentMetadata, PlatformDefaults defaults)
    {
        Dictionary<string, JsonElement> metadata = InheritMetadata(parentMetadata, platform.Metadata);
        PatternEvaluator evaluator = new(repoKey, scopeKey, metadata, [platform]);
        Dictionary<string, Tag> tags = evaluator.ResolveTags(platform.Tags ?? defaults.Tags ?? []);
        string dockerfile = platform.Dockerfile ?? defaults.Dockerfile
            ?? throw new InvalidDataException($"{scopeKey}: dockerfile must be specified explicitly or in defaults.platform.");
        string? dockerfileTemplate = platform.DockerfileTemplate ?? defaults.DockerfileTemplate;

        // Build args are independent of each other, so a platform overrides default args one at a time.
        Dictionary<string, string> buildArgs = new(defaults.BuildArgs ?? [], StringComparer.Ordinal);
        foreach ((string name, string value) in platform.BuildArgs)
        {
            buildArgs[name] = value;
        }

        return platform with
        {
            BuildArgs = buildArgs,
            Dockerfile = evaluator.Expand(dockerfile),
            DockerfileTemplate = dockerfileTemplate is null ? null : evaluator.Expand(dockerfileTemplate),
            Tags = tags,
            // Each platform gets its inherited metadata with patterns expanded for that platform.
            Metadata = evaluator.ExpandMetadata(metadata)
        };
    }

    private static ManifestDefaults InheritDefaults(ManifestDefaults? parentDefaults, ManifestDefaults? defaults) =>
        new()
        {
            Image = new()
            {
                Tags = defaults?.Image.Tags ?? parentDefaults?.Image.Tags
            },
            Platform = new()
            {
                Dockerfile = defaults?.Platform.Dockerfile ?? parentDefaults?.Platform.Dockerfile,
                DockerfileTemplate = defaults?.Platform.DockerfileTemplate ?? parentDefaults?.Platform.DockerfileTemplate,
                BuildArgs = defaults?.Platform.BuildArgs ?? parentDefaults?.Platform.BuildArgs,
                Tags = defaults?.Platform.Tags ?? parentDefaults?.Platform.Tags
            }
        };

    // Replace entire values by top-level key, without changing parent or sibling metadata.
    private static Dictionary<string, JsonElement> InheritMetadata(
        Dictionary<string, JsonElement> parentMetadata, Dictionary<string, JsonElement> metadata)
    {
        Dictionary<string, JsonElement> inheritedMetadata = new(parentMetadata, StringComparer.Ordinal);
        foreach ((string name, JsonElement value) in metadata)
        {
            inheritedMetadata[name] = value;
        }

        return inheritedMetadata;
    }

    // Shared and platform tags share one namespace per repo, across all of its groups.
    private static void ValidateUniqueTags(
        string repoKey, IEnumerable<(string ScopeKey, Dictionary<string, Image> Images)> imageSets)
    {
        Dictionary<string, string> tagOwners = new(StringComparer.Ordinal);
        foreach ((string scopeKey, Dictionary<string, Image> images) in imageSets)
        {
            foreach ((string imageKey, Image image) in images)
            {
                string imageScope = $"{scopeKey}__{imageKey}";
                ClaimTags(image.Tags!, imageScope);
                foreach ((string platformKey, Platform platform) in image.Platforms)
                {
                    ClaimTags(platform.Tags!, $"{imageScope}__{platformKey}");
                }
            }
        }

        void ClaimTags(Dictionary<string, Tag> tags, string scopeKey)
        {
            foreach (string tag in tags.Keys)
            {
                if (!tagOwners.TryAdd(tag, scopeKey))
                {
                    throw new InvalidDataException(
                        $"{repoKey}: tag '{tag}' is declared by both {tagOwners[tag]} and {scopeKey}.");
                }
            }
        }
    }
}

file sealed class PatternEvaluator(
    string repoKey,
    string scopeKey,
    Dictionary<string, JsonElement> metadata,
    Platform[] platforms)
{
    // Placeholders being expanded, to reject metadata values that refer to themselves.
    private readonly HashSet<string> _activePlaceholders = new(StringComparer.Ordinal);

    public string Expand(string pattern)
    {
        string expanded = Regex.Replace(pattern, @"\{([^{}]+)\}", match => ResolvePlaceholder(match.Groups[1].Value));
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
        if (!Regex.IsMatch(name, @"\A[a-zA-Z0-9_][a-zA-Z0-9_.-]{0,127}\z"))
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
}

file static class ManifestYaml
{
    private static readonly IDeserializer s_deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    public static async Task<Manifest> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        string yaml = await File.ReadAllTextAsync(filePath, cancellationToken);
        using StringReader reader = new(yaml);
        MergingParser parser = new(new Parser(reader));
        Dictionary<string, object?> document = s_deserializer.Deserialize<Dictionary<string, object?>>(parser)
            ?? throw new InvalidDataException("The manifest must not be null.");

        // Expand YAML reuse before removing authoring-only definitions and validating the model.
        document.Remove("definitions");
        return ManifestJson.Deserialize(JsonSerializer.SerializeToElement(document));
    }
}

file static class ManifestJson
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { ConfigureProperties }
        },
        Converters =
        {
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false)
        }
    };

    public static Manifest Deserialize(JsonElement document) =>
        document.Deserialize<Manifest>(s_options)
        ?? throw new InvalidDataException("The manifest must not be null.");

    public static Task WriteAsync(string filePath, Manifest manifest, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(manifest, s_options);
        return File.WriteAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
    }

    private static void ConfigureProperties(JsonTypeInfo typeInfo)
    {
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            // Omission enables inheritance; explicit null is not a valid override.
            if (property.PropertyType == typeof(Dictionary<string, Tag>)
                || ((typeInfo.Type == typeof(Platform) || typeInfo.Type == typeof(PlatformDefaults))
                    && property.Name is "dockerfile" or "dockerfileTemplate"))
            {
                property.IsSetNullable = false;
            }

            property.ShouldSerialize = (_, value) =>
                value is not null
                && value is not ICollection { Count: 0 }
                && !(typeInfo.Type == typeof(Tag) && property.Name == "undocumented" && value is false);
        }
    }
}
