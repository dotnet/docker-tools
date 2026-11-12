// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

internal sealed class ManifestResolver(Manifest manifest)
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
        return repo with { Images = images, Groups = groups, Defaults = null };
    }

    private static Group ResolveGroup(
        string repoKey, string scopeKey, Group group,
        Dictionary<string, JsonElement> parentMetadata, ManifestDefaults parentDefaults)
    {
        Dictionary<string, JsonElement> metadata = InheritMetadata(parentMetadata, group.Metadata);
        ManifestDefaults defaults = InheritDefaults(parentDefaults, group.Defaults);
        Dictionary<string, Image> images = ResolveImages(repoKey, scopeKey, group.Images, metadata, defaults);
        return group with { Images = images, Defaults = null };
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
            // Metadata stays where it's declared, so a template's metadata is the manifest, repo, group, image
            // and platform metadata merged by key. A platform also gets each inherited value that has
            // placeholders, expanded for that platform.
            Metadata = evaluator.ExpandMetadata(metadata
                .Where(entry => platform.Metadata.ContainsKey(entry.Key) || HasPlaceholders(entry.Value))
                .ToDictionary(StringComparer.Ordinal))
        };
    }

    private static bool HasPlaceholders(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.ToString().Contains('{'),
        JsonValueKind.Object => value.EnumerateObject().Any(property => HasPlaceholders(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().Any(HasPlaceholders),
        _ => false
    };

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
                ClaimTags(image.Tags, imageScope);
                foreach ((string platformKey, Platform platform) in image.Platforms)
                {
                    ClaimTags(platform.Tags, $"{imageScope}__{platformKey}");
                }
            }
        }

        void ClaimTags(Dictionary<string, Tag>? tags, string scopeKey)
        {
            if (tags is null)
            {
                return;
            }

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
