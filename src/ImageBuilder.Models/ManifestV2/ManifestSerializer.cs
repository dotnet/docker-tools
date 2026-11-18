// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Reads and writes YAML manifests.
/// </summary>
public static partial class ManifestSerializer
{
    // Unknown properties are errors by default.
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnforceRequiredMembers()
        .WithEnforceNullability()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .WithQuotingNecessaryStrings()
        .WithEventEmitter(next => new FlowStringListEmitter(next))
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
            throw new InvalidDataException($"{filePath}: {exception.Message}", exception);
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
                foreach ((string platformKey, Platform platform) in image.Platforms)
                {
                    string platformId = $"{imageId}{ImageEntry.KeySeparator}{platformKey}";
                    if (!OciPlatformRegex().IsMatch(platformKey))
                    {
                        throw new InvalidDataException(
                            $"{platformId}: platforms must be keyed by OCI platform, like 'linux/amd64' or 'linux/arm/v7'.");
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

    [GeneratedRegex(@"^[a-z0-9]+/[a-z0-9]+(/[a-z0-9]+)?$")]
    private static partial Regex OciPlatformRegex();

    // Writes lists of strings, like tags, on one line.
    private sealed class FlowStringListEmitter(IEventEmitter next) : ChainedEventEmitter(next)
    {
        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Type == typeof(string[]))
            {
                eventInfo.Style = SequenceStyle.Flow;
            }

            base.Emit(eventInfo, emitter);
        }
    }
}
