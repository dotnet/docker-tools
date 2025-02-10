// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Kusto.Cloud.Platform.Utils;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

[Export(typeof(ICommand))]
public class GenerateEolAnnotationDataCommand : Command<GenerateEolAnnotationDataOptions, GenerateEolAnnotationDataOptionsBuilder>
{
    private readonly ILoggerService _loggerService;
    private readonly IContainerRegistryClientFactory _acrClientFactory;
    private readonly IContainerRegistryContentClientFactory _acrContentClientFactory;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
    private readonly ILifecycleMetadataService _lifecycleMetadataService;
    private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
    private readonly DateOnly _eolDate;

    [ImportingConstructor]
    public GenerateEolAnnotationDataCommand(
        ILoggerService loggerService,
        IContainerRegistryClientFactory acrClientFactory,
        IContainerRegistryContentClientFactory acrContentClientFactory,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        IRegistryCredentialsProvider registryCredentialsProvider,
        ILifecycleMetadataService lifecycleMetadataService)
    {
        _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        _acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
        _acrContentClientFactory = acrContentClientFactory ?? throw new ArgumentNullException(nameof(acrContentClientFactory));
        _tokenCredentialProvider = tokenCredentialProvider ?? throw new ArgumentNullException(nameof(tokenCredentialProvider));
        _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
        _lifecycleMetadataService = lifecycleMetadataService ?? throw new ArgumentNullException(nameof(lifecycleMetadataService));

        _eolDate = DateOnly.FromDateTime(DateTime.UtcNow); // default EOL date
    }

    protected override string Description => "Generate EOL annotation data";

    public override async Task ExecuteAsync() =>
        await _registryCredentialsProvider.ExecuteWithCredentialsAsync(Options.IsDryRun, async () =>
            {
                List<EolDigestData> digestsToAnnotate = await GetDigestsToAnnotate();
                WriteDigestDataJson(digestsToAnnotate);
            },
            Options.CredentialsOptions,
            registryName: Options.RegistryOptions.Registry,
            ownedAcr: Options.RegistryOptions.Registry);

    private void WriteDigestDataJson(List<EolDigestData> digestsToAnnotate)
    {
        EolAnnotationsData eolAnnotations = new(digestsToAnnotate, _eolDate);

        string annotationsJson = JsonConvert.SerializeObject(
            eolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        File.WriteAllText(Options.EolDigestsListPath, annotationsJson);
    }

    private async Task<List<EolDigestData>> GetDigestsToAnnotate()
    {
        if (!File.Exists(Options.OldImageInfoPath) && !File.Exists(Options.NewImageInfoPath))
        {
            _loggerService.WriteMessage("No digests to annotate because no image info files were provided.");
            return [];
        }

        ImageArtifactDetails oldImageArtifactDetails = ImageInfoHelper.DeserializeImageArtifactDetails(Options.OldImageInfoPath);
        ImageArtifactDetails newImageArtifactDetails = ImageInfoHelper.DeserializeImageArtifactDetails(Options.NewImageInfoPath);

        List<EolDigestData> digestDataList = [];

        try
        {
            // Find all the digests that need to be annotated for EOL by querying the registry for all the digests, scoped to those repos associated with
            // the image info. The repo scoping is done because there may be some cases where multiple image info files are used for different repositories.
            // The intent is to annotate all of the digests that do not exist in the image info file. So this scoping ensures we don't annotate digests that
            // are associated with another image info file. However, we also need to account for the deletion of an entire repository. In that case, we want
            // all the digests in that repo to be annotated. But since the repo is deleted, it doesn't show up in the newly generated image info file. So we
            // need the previous version of the image info file to know that the repo had previously existed and so that repo is included in the scope for
            // the query of the digests.
            IEnumerable<string> repoNames = newImageArtifactDetails.Repos.Select(repo => repo.Repo)
                .Union(oldImageArtifactDetails.Repos.Select(repo => repo.Repo))
                .Select(name => Options.RegistryOptions.RepoPrefix + name);
            Dictionary<string, string?> registryTagsByDigest = await GetAllImageDigestsFromRegistry(repoNames);

            if (!Options.IsDryRun)
            {
                // Only apply the registry override if it's not a dry run. This is because it relies on the input image info file
                // to have populated digest values but that won't be the case in a dry run.
                newImageArtifactDetails = newImageArtifactDetails.ApplyRegistryOverride(Options.RegistryOptions);
            }

            IEnumerable<string> supportedDigests = newImageArtifactDetails.GetAllDigests();

            IEnumerable<EolDigestData> unsupportedDigests = GetUnsupportedDigests(registryTagsByDigest, supportedDigests);

            // Annotate digests that are not already annotated for EOL
            ConcurrentBag<EolDigestData> digetsToAnnotate = [];
            Parallel.ForEach(unsupportedDigests, digest =>
            {
                _loggerService.WriteMessage($"Checking digest for existing annotation: {digest.Digest}");
                if (!_lifecycleMetadataService.IsDigestAnnotatedForEol(digest.Digest, _loggerService, Options.IsDryRun, out _))
                {
                    digetsToAnnotate.Add(digest);
                }
            });

            digestDataList.AddRange(digetsToAnnotate);
        }
        catch (Exception e)
        {
            _loggerService.WriteError($"Error occurred while generating EOL annotation data: {e}");
            throw;
        }

        digestDataList = digestDataList.OrderBy(item => item.Digest).ToList();

        return digestDataList;
    }

    /// <summary>
    /// Finds all the digests that are in the registry but not in the supported digests list.
    /// </summary>
    private static IEnumerable<EolDigestData> GetUnsupportedDigests(
        Dictionary<string, string?> registryTagsByDigest, IEnumerable<string> supportedDigests) =>
        registryTagsByDigest
            .Where(registryDigest => !supportedDigests.Contains(registryDigest.Key))
            .Select(registryDigest => new EolDigestData(registryDigest.Key) { Tag = registryDigest.Value });

    private static string? GetLongestTag(IEnumerable<string> tags) =>
        tags.OrderByDescending(tag => tag.Length).FirstOrDefault();

    private async Task<Dictionary<string, string?>> GetAllImageDigestsFromRegistry(IEnumerable<string> repoNames)
    {
        _loggerService.WriteMessage("Querying registry for all image digests...");

        if (Options.IsDryRun)
        {
            return [];
        }

        IContainerRegistryClient acrClient =
            _acrClientFactory.Create(Options.RegistryOptions.Registry, _tokenCredentialProvider.GetCredential());
        IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNamesAsync();

        ConcurrentBag<(string Digest, string? Tag)> digests = [];
        await foreach (string repositoryName in repositoryNames.Where(name => repoNames.Contains(name)))
        {
            IContainerRegistryContentClient contentClient =
                _acrContentClientFactory.Create(
                    Options.RegistryOptions.Registry,
                    repositoryName,
                    _tokenCredentialProvider.GetCredential());

            ContainerRepository repo = acrClient.GetRepository(repositoryName);
            IAsyncEnumerable<ArtifactManifestProperties> manifests = repo.GetAllManifestPropertiesAsync();
            await foreach (ArtifactManifestProperties manifestProps in manifests)
            {
                ManifestQueryResult manifestResult = await contentClient.GetManifestAsync(manifestProps.Digest);

                // We only want to return image or manifest list digests here. But the registry will also contain digests for annotations.
                // These annotation digests should not be returned as we don't want to annotate an annotation. An annotation is just a referrer
                // and referrers are indicated by the presence of a subject field. So skip any manifest that has a subject field.
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
}
