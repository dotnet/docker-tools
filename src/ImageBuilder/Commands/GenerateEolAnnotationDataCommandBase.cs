// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands;


public abstract class GenerateEolAnnotationDataCommandBase<TOptions, TOptionsBuilder>
    : Command<TOptions, TOptionsBuilder>
    where TOptions : GenerateEolAnnotationDataOptions, new()
    where TOptionsBuilder : GenerateEolAnnotationDataOptionsBuilder, new()
{
    private readonly IAcrContentClientFactory _acrContentClientFactory;
    private readonly IAcrClientFactory _acrClientFactory;
    private readonly ILifecycleMetadataService _lifecycleMetadataService;
    private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
    private readonly DateOnly _eolDate = DateOnly.FromDateTime(DateTime.UtcNow); // default EOL date

    protected GenerateEolAnnotationDataCommandBase(
        ILogger logger,
        IAcrContentClientFactory acrContentClientFactory,
        IAcrClientFactory acrClientFactory,
        ILifecycleMetadataService lifecycleMetadataService,
        IRegistryCredentialsProvider registryCredentialsProvider)
    {
        LoggerService = logger;
        _acrContentClientFactory = acrContentClientFactory;
        _acrClientFactory = acrClientFactory;
        _lifecycleMetadataService = lifecycleMetadataService;
        _registryCredentialsProvider = registryCredentialsProvider;
    }

    protected ILogger LoggerService { get; }

    public sealed override async Task ExecuteAsync()
    {
        IEnumerable<EolDigestData> digestsToAnnotate = [];
        await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
            Options.IsDryRun,
            async () => digestsToAnnotate = GetDigestsWithoutExistingAnnotation(await GetDigestsToAnnotateAsync()),
            Options.CredentialsOptions,
            registryName: Options.RegistryOptions.Registry);

        WriteDigestDataJson(digestsToAnnotate);
    }

    protected abstract Task<IEnumerable<EolDigestData>> GetDigestsToAnnotateAsync();

    protected async Task<IEnumerable<EolDigestData>> GetAllImageDigestsFromRegistryAsync(
        Func<string, bool>? repoNameFilter = null)
    {
        LoggerService.LogInformation("Querying registry for all image digests...");

        if (Options.IsDryRun)
        {
            return [];
        }

        IAcrClient acrClient = _acrClientFactory.Create(Options.RegistryOptions.Registry);

        IAsyncEnumerable<string> repositoryNames =
            acrClient.GetRepositoryNamesAsync()
                     .Where(repo => repoNameFilter is null || repoNameFilter(repo));

        ConcurrentBag<(string Digest, string? Tag)> digests = [];
        await Parallel.ForEachAsync(repositoryNames, async (repositoryName, outerCT) =>
        {
            IAcrContentClient contentClient =
                _acrContentClientFactory.Create(
                    Acr.Parse(Options.RegistryOptions.Registry),
                    repositoryName);

            ContainerRepository repo = acrClient.GetRepository(repositoryName);
            IAsyncEnumerable<ArtifactManifestProperties> manifests = repo.GetAllManifestPropertiesAsync();
            await Parallel.ForEachAsync(manifests, outerCT, async (manifestProps, innerCT) =>
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
            });
        });

        return digests
            .Select(val => new EolDigestData(val.Digest) { Tag = val.Tag });
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

    private IEnumerable<EolDigestData> GetDigestsWithoutExistingAnnotation(
        IEnumerable<EolDigestData> unsupportedDigests)
    {
        // Annotate digests that are not already annotated for EOL
        ConcurrentBag<EolDigestData> digestsToAnnotate = [];
        Parallel.ForEach(unsupportedDigests, digest =>
        {
            LoggerService.LogInformation($"Checking digest for existing annotation: {digest.Digest}");
            if (!_lifecycleMetadataService.IsDigestAnnotatedForEol(digest.Digest, LoggerService, Options.IsDryRun, out _))
            {
                digestsToAnnotate.Add(digest);
            }
        });

        return digestsToAnnotate
            .OrderBy(item => item.Digest)
            .ToList();
    }

    private static string? GetLongestTag(IEnumerable<string> tags) =>
        tags.OrderByDescending(tag => tag.Length).FirstOrDefault();
}
