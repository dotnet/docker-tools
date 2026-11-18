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
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnforceRequiredMembers()
        .WithEnforceNullability()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
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
                HashSet<string> platforms = [];
                foreach ((string platformKey, ImagePlatform imagePlatform) in image.Platforms)
                {
                    string platformId = $"{imageId}{ImageEntry.KeySeparator}{platformKey}";
                    if (!PlatformRegex.IsMatch(imagePlatform.Platform))
                    {
                        throw new InvalidDataException(
                            $"{platformId}: platform '{imagePlatform.Platform}' must look like 'linux/amd64' or 'linux/arm/v7'.");
                    }

                    if (!platforms.Add(imagePlatform.Platform))
                    {
                        throw new InvalidDataException(
                            $"{imageId}: more than one platform is {imagePlatform.Platform}.");
                    }

                    ClaimTags([.. imagePlatform.Tags, .. imagePlatform.UndocumentedTags], platformId);
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
    private static partial Regex PlatformRegex { get; }

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
    }
}
