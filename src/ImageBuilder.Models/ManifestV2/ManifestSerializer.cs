// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Reads and writes YAML manifests.
/// </summary>
public static class ManifestSerializer
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnumNamingConvention(LowerCaseNamingConvention.Instance)
        .WithEnforceRequiredMembers()
        .WithEnforceNullability()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnumNamingConvention(LowerCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .DisableAliases()
        .WithQuotingNecessaryStrings()
        .WithEventEmitter(next => new FlowStyleEmitter(next))
        .Build();

    /// <summary>
    /// Reads and validates the manifest at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The manifest is invalid.</exception>
    public static Manifest Load(string filePath)
    {
        string yaml = File.ReadAllText(filePath);
        Manifest manifest;
        try
        {
            manifest = YamlDeserializer.Deserialize<Manifest?>(yaml)
                ?? throw new InvalidDataException($"{filePath}: the manifest must not be empty.");
        }
        catch (YamlException exception)
        {
            string detail = exception.InnerException is null ? "" : $" {exception.InnerException.Message}";
            throw new InvalidDataException($"{filePath}: {exception.Message}{detail}", exception);
        }

        Validate(manifest);
        return manifest;
    }

    /// <summary>
    /// Writes <paramref name="manifest"/> as YAML, leaving out empty and unset values.
    /// </summary>
    public static string Serialize(Manifest manifest) => YamlSerializer.Serialize(manifest);

    private static void Validate(Manifest manifest)
    {
        foreach ((string repoKey, Repo repo) in manifest.Repos)
        {
            Dictionary<string, string> tagOwners = new(StringComparer.Ordinal);
            foreach ((string imageKey, Image image) in repo.Images)
            {
                string imageId = $"{repoKey}{ImageEntry.KeySeparator}{imageKey}";
                if (image.Platforms.Count == 0)
                {
                    throw new InvalidDataException($"{imageId}: an image must have at least one platform.");
                }

                ClaimTags(image.SharedTags, imageId);
                HashSet<OciPlatform> targetPlatforms = [];
                foreach ((string platformKey, Platform platform) in image.Platforms)
                {
                    string platformId = $"{imageId}{ImageEntry.KeySeparator}{platformKey}";
                    if (!targetPlatforms.Add(platform.TargetPlatform))
                    {
                        throw new InvalidDataException(
                            $"{imageId}: more than one platform targets {platform.TargetPlatform}.");
                    }

                    ClaimTags([.. platform.Tags, .. platform.UndocumentedTags], platformId);
                }
            }

            void ClaimTags(string[] tags, string ownerId)
            {
                foreach (string tag in tags)
                {
                    if (!tagOwners.TryAdd(tag, ownerId))
                    {
                        throw new InvalidDataException(
                            $"{repoKey}: tag '{tag}' is declared by both {tagOwners[tag]} and {ownerId}.");
                    }
                }
            }
        }
    }

    private sealed class FlowStyleEmitter(IEventEmitter next) : ChainedEventEmitter(next)
    {
        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Type == typeof(string[]))
            {
                eventInfo.Style = SequenceStyle.Flow;
            }

            base.Emit(eventInfo, emitter);
        }

        public override void Emit(MappingStartEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Type == typeof(OciPlatform))
            {
                eventInfo.Style = MappingStyle.Flow;
            }

            base.Emit(eventInfo, emitter);
        }
    }
}
