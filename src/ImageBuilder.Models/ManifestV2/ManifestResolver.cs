// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

internal sealed class ManifestResolver(Manifest manifest)
{
    public Manifest Resolve()
    {
        Dictionary<string, Repo> repos = [];
        foreach ((string repoKey, Repo repo) in manifest.Repos)
        {
            repos[repoKey] = ResolveRepo(repoKey, repo, manifest.Metadata, manifest.Defaults);
        }

        return manifest with { Repos = repos, Defaults = null };
    }

    private static Repo ResolveRepo(
        string repoKey, Repo repo, Dictionary<string, JsonElement> parentMetadata, ManifestDefaults? parentDefaults)
    {
        Dictionary<string, JsonElement> inheritedMetadata = InheritMetadata(parentMetadata, repo.Metadata);
        ManifestDefaults inheritedDefaults = InheritDefaults(parentDefaults, repo.Defaults);
        Dictionary<string, Image> images = ResolveImages(repoKey, repoKey, repo.Images, inheritedMetadata, inheritedDefaults);
        List<(string ScopeKey, Dictionary<string, Image> Images)> imageSets = [(repoKey, images)];

        Dictionary<string, Group> groups = [];
        foreach ((string groupKey, Group group) in repo.Groups)
        {
            string scopeKey = $"{repoKey}__{groupKey}";
            Group resolvedGroup = ResolveGroup(repoKey, scopeKey, group, inheritedMetadata, inheritedDefaults);
            groups[groupKey] = resolvedGroup;
            imageSets.Add((scopeKey, resolvedGroup.Images));
        }

        ValidateUniqueTags(repoKey, imageSets);
        return repo with { Images = images, Groups = groups, Defaults = null };
    }

    private static Group ResolveGroup(
        string repoKey, string scopeKey, Group group,
        Dictionary<string, JsonElement> parentMetadata, ManifestDefaults parentDefaults)
    {
        Dictionary<string, JsonElement> inheritedMetadata = InheritMetadata(parentMetadata, group.Metadata);
        ManifestDefaults inheritedDefaults = InheritDefaults(parentDefaults, group.Defaults);
        Dictionary<string, Image> images = ResolveImages(repoKey, scopeKey, group.Images, inheritedMetadata, inheritedDefaults);
        return group with { Images = images, Defaults = null };
    }

    private static Dictionary<string, Image> ResolveImages(
        string repoKey, string scopeKey, Dictionary<string, Image> images,
        Dictionary<string, JsonElement> parentMetadata, ManifestDefaults parentDefaults)
    {
        Dictionary<string, Image> resolvedImages = [];
        foreach ((string imageKey, Image image) in images)
        {
            string imageScopeKey = $"{scopeKey}__{imageKey}";
            resolvedImages[imageKey] = ResolveImage(repoKey, imageScopeKey, image, parentMetadata, parentDefaults);
        }

        return resolvedImages;
    }

    private static Image ResolveImage(
        string repoKey, string scopeKey, Image image,
        Dictionary<string, JsonElement> parentMetadata, ManifestDefaults parentDefaults)
    {
        Dictionary<string, JsonElement> inheritedMetadata = InheritMetadata(parentMetadata, image.Metadata);
        ManifestDefaults inheritedDefaults = InheritDefaults(parentDefaults, image.Defaults);
        Platform[] imagePlatforms = [.. image.Platforms.Values];
        PatternEvaluator evaluator = new(repoKey, scopeKey, inheritedMetadata, imagePlatforms);
        Dictionary<string, Tag> tagPatterns = image.Tags ?? inheritedDefaults.Image.Tags ?? [];
        Dictionary<string, Tag> tags = evaluator.ResolveTags(tagPatterns);

        Dictionary<string, Platform> platforms = [];
        foreach ((string platformKey, Platform platform) in image.Platforms)
        {
            string platformScopeKey = $"{scopeKey}__{platformKey}";
            platforms[platformKey] = ResolvePlatform(repoKey, platformScopeKey, platform, inheritedMetadata, inheritedDefaults.Platform);
        }

        return image with
        {
            Tags = tags,
            Platforms = platforms,
            Defaults = null
        };
    }

    private static Platform ResolvePlatform(
        string repoKey, string scopeKey, Platform platform,
        Dictionary<string, JsonElement> parentMetadata, PlatformDefaults inheritedDefaults)
    {
        Dictionary<string, JsonElement> inheritedMetadata = InheritMetadata(parentMetadata, platform.Metadata);
        PatternEvaluator evaluator = new(repoKey, scopeKey, inheritedMetadata, [platform]);
        Dictionary<string, Tag> tagPatterns = platform.Tags ?? inheritedDefaults.Tags ?? [];
        Dictionary<string, Tag> tags = evaluator.ResolveTags(tagPatterns);

        string dockerfilePattern = platform.Dockerfile
            ?? inheritedDefaults.Dockerfile
            ?? throw new InvalidDataException(
                $"{scopeKey}: dockerfile must be specified explicitly or in defaults.platform.");
        string dockerfilePath = evaluator.Expand(dockerfilePattern);

        string? dockerfileTemplatePattern = platform.DockerfileTemplate ?? inheritedDefaults.DockerfileTemplate;
        string? dockerfileTemplatePath = null;
        if (dockerfileTemplatePattern is not null)
        {
            dockerfileTemplatePath = evaluator.Expand(dockerfileTemplatePattern);
        }

        // Build args are independent of each other, so a platform overrides default args one at a time.
        Dictionary<string, string> defaultBuildArgs = inheritedDefaults.BuildArgs ?? [];
        Dictionary<string, string> buildArgs = new(defaultBuildArgs, StringComparer.Ordinal);
        foreach ((string name, string value) in platform.BuildArgs)
        {
            buildArgs[name] = value;
        }

        // Metadata stays where it's declared, so a template's metadata is the manifest, repo, group, image
        // and platform metadata merged by key. A platform also gets each inherited value that has
        // placeholders, expanded for that platform.
        Dictionary<string, JsonElement> platformMetadata = new(StringComparer.Ordinal);
        foreach ((string key, JsonElement value) in inheritedMetadata)
        {
            if (platform.Metadata.ContainsKey(key) || HasPlaceholders(value))
            {
                platformMetadata[key] = value;
            }
        }

        Dictionary<string, JsonElement> expandedMetadata = evaluator.ExpandMetadata(platformMetadata);

        return platform with
        {
            BuildArgs = buildArgs,
            Dockerfile = dockerfilePath,
            DockerfileTemplate = dockerfileTemplatePath,
            Tags = tags,
            Metadata = expandedMetadata
        };
    }

    private static bool HasPlaceholders(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.ToString().Contains('{'),
        JsonValueKind.Object => value.EnumerateObject().Any(HasPlaceholders),
        JsonValueKind.Array => value.EnumerateArray().Any(HasPlaceholders),
        _ => false
    };

    private static bool HasPlaceholders(JsonProperty property) => HasPlaceholders(property.Value);

    private static ManifestDefaults InheritDefaults(ManifestDefaults? parentDefaults, ManifestDefaults? defaults)
    {
        ImageDefaults? image = defaults?.Image;
        ImageDefaults? parentImage = parentDefaults?.Image;
        PlatformDefaults? platform = defaults?.Platform;
        PlatformDefaults? parentPlatform = parentDefaults?.Platform;

        ImageDefaults inheritedImage = new()
        {
            Tags = image?.Tags ?? parentImage?.Tags
        };

        PlatformDefaults inheritedPlatform = new()
        {
            Dockerfile = platform?.Dockerfile ?? parentPlatform?.Dockerfile,
            DockerfileTemplate = platform?.DockerfileTemplate ?? parentPlatform?.DockerfileTemplate,
            BuildArgs = platform?.BuildArgs ?? parentPlatform?.BuildArgs,
            Tags = platform?.Tags ?? parentPlatform?.Tags
        };

        return new ManifestDefaults
        {
            Image = inheritedImage,
            Platform = inheritedPlatform
        };
    }

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
                string imageScopeKey = $"{scopeKey}__{imageKey}";
                ClaimTags(image.Tags, imageScopeKey);
                foreach ((string platformKey, Platform platform) in image.Platforms)
                {
                    string platformScopeKey = $"{imageScopeKey}__{platformKey}";
                    ClaimTags(platform.Tags, platformScopeKey);
                }
            }
        }

        void ClaimTags(Dictionary<string, Tag>? tags, string ownerKey)
        {
            if (tags is null)
            {
                return;
            }

            foreach (string tag in tags.Keys)
            {
                if (!tagOwners.TryAdd(tag, ownerKey))
                {
                    throw new InvalidDataException(
                        $"{repoKey}: tag '{tag}' is declared by both {tagOwners[tag]} and {ownerKey}.");
                }
            }
        }
    }
}
