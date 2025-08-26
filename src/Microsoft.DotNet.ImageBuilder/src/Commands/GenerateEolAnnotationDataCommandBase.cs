// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable

public abstract class GenerateEolAnnotationDataCommandBase<TOptions, TOptionsBuilder>
    : Command<TOptions, TOptionsBuilder>
    where TOptions : GenerateEolAnnotationDataOptions, new()
    where TOptionsBuilder : GenerateEolAnnotationDataOptionsBuilder, new()
{
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
    private readonly IContainerRegistryContentClientFactory _acrContentClientFactory;
    private readonly IContainerRegistryClientFactory _acrClientFactory;
    private readonly ILifecycleMetadataService _lifecycleMetadataService;
    private readonly DateOnly _eolDate = DateOnly.FromDateTime(DateTime.UtcNow); // default EOL date

    protected GenerateEolAnnotationDataCommandBase(
        ILoggerService loggerService,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        IContainerRegistryContentClientFactory acrContentClientFactory,
        IContainerRegistryClientFactory acrClientFactory,
        ILifecycleMetadataService lifecycleMetadataService)
    {
        LoggerService = loggerService;
        _tokenCredentialProvider = tokenCredentialProvider;
        _acrContentClientFactory = acrContentClientFactory;
        _acrClientFactory = acrClientFactory;
        _lifecycleMetadataService = lifecycleMetadataService;
    }

    protected ILoggerService LoggerService { get; }

    protected async Task<Dictionary<string, string?>> GetAllImageDigestsFromRegistryAsync(
        Func<string, bool>? repoNameFilter = null)
    {
        LoggerService.WriteMessage("Querying registry for all image digests...");

        if (Options.IsDryRun)
        {
            return [];
        }

        var credential = _tokenCredentialProvider.GetCredential(Options.AcrServiceConnection);
        IContainerRegistryClient acrClient =
            _acrClientFactory.Create(Options.RegistryOptions.Registry, credential);
        IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNamesAsync();

        ConcurrentBag<(string Digest, string? Tag)> digests = [];
        await foreach (string repositoryName in repositoryNames
            .Where(repo => repoNameFilter is null || repoNameFilter(repo)))
        {
            IContainerRegistryContentClient contentClient =
                _acrContentClientFactory.Create(
                    Options.RegistryOptions.Registry,
                    repositoryName,
                    Options.AcrServiceConnection);

            ContainerRepository repo = acrClient.GetRepository(repositoryName);
            IAsyncEnumerable<ArtifactManifestProperties> manifests = repo.GetAllManifestPropertiesAsync();
            await foreach (ArtifactManifestProperties manifestProps in manifests)
            {
                ManifestQueryResult manifestResult = await contentClient.GetManifestAsync(manifestProps.Digest);

                // We only want to return image or manifest list digests here. But the registry will also contain
                // digests for annotations. These annotation digests should not be returned as we don't want to
                // annotate an annotation. An annotation is just a referrer and referrers are indicated by the presence
                // of a subject field. So skip any manifest that has a subject field.
                if (manifestResult.Manifest["subject"] is null)
                {
                    string imageName = DockerHelper.GetImageName(
                        registry: Options.RegistryOptions.Registry,
                        repo: repositoryName,
                        digest: manifestProps.Digest);
                    digests.Add((imageName, GetLongestTag(manifestProps.Tags)));
                }
            }
        }

        return digests.ToDictionary(val => val.Digest, val => val.Tag);
    }

    /// <summary>
    /// Finds all the digests that are in the registry but not in the supported digests list.
    /// </summary>
    protected static IEnumerable<EolDigestData> GetUnsupportedDigests(
        Dictionary<string, string?> registryTagsByDigest, IEnumerable<string> supportedDigests) =>
        registryTagsByDigest
            .Where(registryDigest => !supportedDigests.Contains(registryDigest.Key))
            .Select(registryDigest => new EolDigestData(registryDigest.Key) { Tag = registryDigest.Value });

    protected IEnumerable<EolDigestData> GetDigestsToAnnotate(IEnumerable<EolDigestData> unsupportedDigests)
    {
        // Annotate digests that are not already annotated for EOL
        ConcurrentBag<EolDigestData> digestsToAnnotate = [];
        Parallel.ForEach(unsupportedDigests, digest =>
        {
            LoggerService.WriteMessage($"Checking digest for existing annotation: {digest.Digest}");
            if (!_lifecycleMetadataService.IsDigestAnnotatedForEol(digest.Digest, LoggerService, Options.IsDryRun, out _))
            {
                digestsToAnnotate.Add(digest);
            }
        });

        return digestsToAnnotate
            .OrderBy(item => item.Digest)
            .ToList();
    }

    protected void WriteDigestDataJson(IEnumerable<EolDigestData> digestsToAnnotate)
    {
        EolAnnotationsData eolAnnotations = new(digestsToAnnotate.ToList(), _eolDate);

        string annotationsJson = JsonConvert.SerializeObject(
            eolAnnotations,
            Formatting.Indented,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        File.WriteAllText(Options.EolDigestsListPath, annotationsJson);
    }

    private static string? GetLongestTag(IEnumerable<string> tags) =>
        tags.OrderByDescending(tag => tag.Length).FirstOrDefault();
}
