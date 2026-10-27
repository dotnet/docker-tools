// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:project ..\ImageBuilder.Models\Microsoft.DotNet.ImageBuilder.Models.csproj
#:property PublishAot=false

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

// Run from this directory: dotnet run --file Program.cs
if (args.Length > 1)
{
    throw new ArgumentException("Expected at most one argument: the manifest path.");
}

string inputPath = args.Length == 1 ? args[0] : "manifest.json";
string outputPath = Path.ChangeExtension(inputPath, ".resolved.json");

Manifest manifest = await ManifestJson.ReadAsync(inputPath);
Manifest resolved = new ManifestResolver(manifest).Resolve();
await ManifestJson.WriteAsync(outputPath, resolved);
Console.WriteLine($"Wrote {outputPath}");

file sealed class ManifestResolver(Manifest manifest)
{
    public Manifest Resolve()
    {
        ResolutionScope scope = new(manifest.Metadata, manifest.TagPatterns);
        Dictionary<string, Repo> repos = manifest.Repos.ToDictionary(
            repoEntry => repoEntry.Key,
            repoEntry => ResolveRepo(repoEntry.Key, repoEntry.Value, scope));

        return manifest with { Repos = repos, TagPatterns = new() };
    }

    private Repo ResolveRepo(string key, Repo repo, ResolutionScope parent)
    {
        ResolutionScope scope = parent.Enter(key, repo.Metadata, repo.TagPatterns);
        Dictionary<string, Image> images = repo.Images.ToDictionary(
            imageEntry => imageEntry.Key,
            imageEntry => ResolveImage(imageEntry.Key, imageEntry.Value, scope));

        return repo with { Metadata = scope.Metadata, Images = images, TagPatterns = new() };
    }

    private Image ResolveImage(string key, Image image, ResolutionScope parent)
    {
        ResolutionScope scope = parent.Enter(key, image.Metadata, image.TagPatterns);
        TagEvaluator evaluator = new(scope, image.ProductVersion);
        Tag[] tags = evaluator.Resolve(scope.TagPatterns.ImageTags, image.Tags);
        Dictionary<string, Platform> platforms = image.Platforms.ToDictionary(
            platformEntry => platformEntry.Key,
            platformEntry => ResolvePlatform(
                platformEntry.Key, platformEntry.Value, scope, image.ProductVersion));

        return image with
        {
            Metadata = evaluator.Metadata,
            Tags = tags,
            Platforms = platforms,
            TagPatterns = new()
        };
    }

    private Platform ResolvePlatform(
        string key, Platform platform, ResolutionScope parent, string? productVersion)
    {
        ResolutionScope scope = parent.Enter(key, platform.Metadata);
        string osKey = platform.TargetPlatform.OsDefinition;
        if (!manifest.OsDefinitions.TryGetValue(osKey, out OsDefinition? os))
        {
            throw new InvalidDataException($"{scope.ScopeKey}: unknown OS definition '{osKey}'.");
        }

        TagEvaluator evaluator = new(scope, productVersion, os, platform.TargetPlatform.Arch);
        Tag[] tags = evaluator.Resolve(scope.TagPatterns.PlatformTags, platform.Tags);

        return platform with { Metadata = evaluator.Metadata, Tags = tags };
    }
}

file sealed class ResolutionScope(
    Dictionary<string, JsonElement> metadata,
    TagPatterns tagPatterns,
    string scopeKey = "")
{
    public Dictionary<string, JsonElement> Metadata { get; } = metadata;
    public TagPatterns TagPatterns { get; } = tagPatterns;
    public string ScopeKey { get; } = scopeKey;

    /// <summary>
    /// Inherits all tag patterns and metadata keys. Child metadata replaces values by top-level key,
    /// including entire nested objects and arrays; it does not deep-merge them.
    /// </summary>
    public ResolutionScope Enter(string key, Dictionary<string, JsonElement> metadata, TagPatterns? tagPatterns = null)
    {
        // Copy parent metadata so child overrides cannot affect its parent or siblings.
        Dictionary<string, JsonElement> inheritedMetadata = new(Metadata, StringComparer.Ordinal);
        foreach ((string name, JsonElement value) in metadata)
        {
            inheritedMetadata[name] = value;
        }

        // Append child patterns, keeping all inherited patterns available to descendants.
        TagPatterns inheritedPatterns = tagPatterns is null ? TagPatterns : new TagPatterns
        {
            ImageTags = [.. TagPatterns.ImageTags, .. tagPatterns.ImageTags],
            PlatformTags = [.. TagPatterns.PlatformTags, .. tagPatterns.PlatformTags]
        };

        // Extend the scope key, e.g. dotnet__aspnet__nanoserver2022-amd64.
        string childScopeKey = ScopeKey.Length == 0 ? key : $"{ScopeKey}__{key}";

        return new ResolutionScope(inheritedMetadata, inheritedPatterns, childScopeKey);
    }
}

file sealed class TagEvaluator
{
    private readonly string _scopeKey;

    public TagEvaluator(ResolutionScope scope, string? productVersion)
    {
        _scopeKey = scope.ScopeKey;
        Metadata = new Dictionary<string, JsonElement>(scope.Metadata, StringComparer.Ordinal);
        if (productVersion is not null)
        {
            AddBuiltIn("productVersion", productVersion);
        }
    }

    public TagEvaluator(ResolutionScope scope, string? productVersion, OsDefinition os, Arch arch)
        : this(scope, productVersion)
    {
        AddBuiltIn("os", os.TagPart);
        AddBuiltIn("arch", JsonNamingPolicy.CamelCase.ConvertName(arch.ToString()));
    }

    public Dictionary<string, JsonElement> Metadata { get; }

    public Tag[] Resolve(Tag[] patterns, Tag[] explicitTags) =>
        patterns.Concat(explicitTags).Select(ExpandTag).ToArray();

    private Tag ExpandTag(Tag tag)
    {
        string name = Regex.Replace(tag.Name, @"\{([^{}]+)\}", match => ResolvePlaceholder(match.Groups[1].Value));
        if (!Regex.IsMatch(name, @"\A[a-zA-Z0-9_][a-zA-Z0-9_.-]{0,127}\z"))
        {
            throw new InvalidDataException($"{_scopeKey}: resolved tag '{name}' is not a valid Docker tag.");
        }

        return tag with { Name = name };
    }

    private string ResolvePlaceholder(string placeholder)
    {
        string[] segments = placeholder.Split('.');
        if (!Metadata.TryGetValue(segments[0], out JsonElement value))
        {
            throw new InvalidDataException($"{_scopeKey}: unknown tag variable '{placeholder}'.");
        }

        foreach (string segment in segments.Skip(1))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                throw new InvalidDataException($"{_scopeKey}: unknown tag variable '{placeholder}'.");
            }
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.ToString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => throw new InvalidDataException($"{_scopeKey}: tag variable '{placeholder}' must be a non-null scalar.")
        };
    }

    private void AddBuiltIn(string name, string value)
    {
        if (!Metadata.TryAdd(name, JsonSerializer.SerializeToElement(value)))
        {
            throw new InvalidDataException($"Metadata cannot redefine the built-in variable '{name}'.");
        }
    }
}

file static class ManifestJson
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { OmitEmptyProperties }
        },
        Converters =
        {
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false)
        }
    };

    public static async Task<Manifest> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<Manifest>(json, s_options)
            ?? throw new InvalidDataException("The manifest must not be null.");
    }

    public static Task WriteAsync(string filePath, Manifest manifest, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(manifest, s_options);
        return File.WriteAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
    }

    private static void OmitEmptyProperties(JsonTypeInfo typeInfo)
    {
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            property.ShouldSerialize = (_, value) =>
                value is not null
                && value is not ICollection { Count: 0 }
                && value is not TagPatterns { ImageTags.Length: 0, PlatformTags.Length: 0 }
                && !(typeInfo.Type == typeof(Tag) && property.Name == "undocumented" && value is false);
        }
    }
}
