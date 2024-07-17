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
    private readonly IDotNetReleasesService _dotNetReleasesService;
    private readonly ILoggerService _loggerService;
    private readonly IContainerRegistryClientFactory _acrClientFactory;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
    private readonly IOrasService _orasService;
    private readonly DateOnly _eolDate;

    private Dictionary<string, DateOnly?> _productEolDates = null!;
    private ImageArtifactDetails _oldImageArtifactDetails = null!;
    private ImageArtifactDetails _newImageArtifactDetails = null!;

    [ImportingConstructor]
    public GenerateEolAnnotationDataCommand(
        IDotNetReleasesService dotNetReleasesService,
        ILoggerService loggerService,
        IContainerRegistryClientFactory acrClientFactory,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        IOrasService orasService)
    {
        _dotNetReleasesService = dotNetReleasesService ?? throw new ArgumentNullException(nameof(dotNetReleasesService));
        _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        _acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
        _tokenCredentialProvider = tokenCredentialProvider ?? throw new ArgumentNullException(nameof(tokenCredentialProvider));
        _orasService = orasService ?? throw new ArgumentNullException(nameof(orasService));

        _eolDate = DateOnly.FromDateTime(DateTime.UtcNow); // default EOL date
    }

    protected override string Description => "Generate EOL annotation data";

    public override async Task ExecuteAsync()
    {
        _productEolDates = await _dotNetReleasesService.GetProductEolDatesFromReleasesJson();

        _oldImageArtifactDetails = LoadImageInfoData(Options.OldImageInfoPath);
        _newImageArtifactDetails = LoadImageInfoData(Options.NewImageInfoPath);

        List<EolDigestData> digestsToAnnotate = await GetDigestsToAnnotate();
        SerializeDigestDataJson(digestsToAnnotate);
    }

    private void SerializeDigestDataJson(List<EolDigestData> digestsToAnnotate)
    {
        EolAnnotationsData eolAnnotations = new(digestsToAnnotate, _eolDate);

        string annotationsJson = JsonConvert.SerializeObject(eolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        File.WriteAllText(Options.EolDigestsListPath, annotationsJson);
    }

    private async Task<List<EolDigestData>> GetDigestsToAnnotate()
    {
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
            IEnumerable<string> repoNames = _newImageArtifactDetails.Repos.Select(repo => repo.Repo)
                .Union(_oldImageArtifactDetails.Repos.Select(repo => repo.Repo));
            IEnumerable<(string Digest, string? Tag)> registryDigests = await GetAllDigestsFromRegistry(repoNames);

            IEnumerable<string> supportedDigests = GetSupportedDigests();
            IEnumerable<EolDigestData> unsupportedDigests = GetUnsupportedDigests(registryDigests, supportedDigests);

            // Annotate digests that are not already annotated for EOL
            ConcurrentBag<EolDigestData> digetsToAnnotate = [];
            Parallel.ForEach(unsupportedDigests, digest =>
            {
                if (!_orasService.IsDigestAnnotatedForEol(digest.Digest, _loggerService, Options.IsDryRun))
                {
                    digetsToAnnotate.Add(digest);
                }
            });

            digestDataList.AddRange(digetsToAnnotate);

            if (Options.AnnotateEolProducts)
            {
                // Annotate images for eol products in new image info
                foreach (ImageData image in _newImageArtifactDetails.Repos.SelectMany(repo => repo.Images))
                {
                    digestDataList.AddRange(GetProductEolDigests(image));
                }
            }
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
    private static IEnumerable<EolDigestData> GetUnsupportedDigests(IEnumerable<(string Digest, string? Tag)> registryDigests, IEnumerable<string> supportedDigests)
    {
        List<(string Digest, string? Tag)> sortedRegistryDigests = registryDigests
            .OrderBy(val => val.Digest)
            .ToList();
        List<string> sortedSupportedDigests = supportedDigests
            .Order()
            .ToList();

        int supportedIndex = 0;
        int registryIndex = 0;
        List<EolDigestData> result = [];
        while (supportedIndex < sortedSupportedDigests.Count && registryIndex < sortedRegistryDigests.Count)
        {
            int comparison = sortedSupportedDigests[supportedIndex].CompareTo(sortedRegistryDigests[registryIndex].Digest);
            if (comparison < 0)
            {
                supportedIndex++;
            }
            else if (comparison > 0)
            {
                (string digest, string? tag) = sortedRegistryDigests[registryIndex];
                result.Add(new EolDigestData(digest) { Tag = tag });
                registryIndex++;
            }
            else
            {
                supportedIndex++;
                registryIndex++;
            }
        }

        // If there are remaining items, add them to the result
        while (registryIndex < sortedRegistryDigests.Count)
        {
            (string digest, string tag) = sortedRegistryDigests[registryIndex];
            result.Add(new EolDigestData(digest) { Tag = tag });
            registryIndex++;
        }

        return result;
    }

    private IEnumerable<string> GetSupportedDigests() =>
        _newImageArtifactDetails.Repos
            .SelectMany(repo => repo.Images)
            .SelectMany(image => GetImageDigests(image))
            .Select(digest => digest.Digest);
    
    private static IEnumerable<(string Digest, string? Tag)> GetImageDigests(ImageData image)
    {
        if (image.Manifest is not null)
        {
            yield return (image.Manifest.Digest, GetLongestTag(image.Manifest.SharedTags));
        }

        foreach (PlatformData platform in image.Platforms)
        {
            yield return (platform.Digest, GetLongestTag(platform.SimpleTags));
        }
    }

    private static string? GetLongestTag(IEnumerable<string> tags) =>
        tags.OrderByDescending(tag => tag.Length).FirstOrDefault();

    private async Task<IEnumerable<(string Digest, string? Tag)>> GetAllDigestsFromRegistry(IEnumerable<string> repoNames)
    {
        IContainerRegistryClient acrClient = _acrClientFactory.Create(Options.RegistryName, _tokenCredentialProvider.GetCredential());
        IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNamesAsync();

        ConcurrentBag<(string Digest, string? Tag)> digests = [];
        await foreach (string repositoryName in repositoryNames.Where(name => repoNames.Contains(name)))
        {
            ContainerRepository repo = acrClient.GetRepository(repositoryName);
            IAsyncEnumerable<ArtifactManifestProperties> manifests = repo.GetAllManifestPropertiesAsync();
            await foreach (ArtifactManifestProperties manifestProps in manifests)
            {
                string imageName = DockerHelper.GetImageName(Options.RegistryName, repositoryName, digest: manifestProps.Digest);
                digests.Add((imageName, GetLongestTag(manifestProps.Tags)));
            }
        }

        return digests;
    }

    private IEnumerable<EolDigestData> GetProductEolDigests(ImageData image)
    {
        if (image.ProductVersion != null)
        {
            string dotnetVersion = Version.Parse(image.ProductVersion).ToString(2);
            if (_productEolDates != null && _productEolDates.TryGetValue(dotnetVersion, out DateOnly? date))
            {
                return GetImageDigests(image).Select(val => new EolDigestData(val.Digest) { Tag = val.Tag, EolDate = date });
            }
        }

        return [];
    }

    private static ImageArtifactDetails LoadImageInfoData(string imageInfoPath)
    {
        string imageInfoJson = File.ReadAllText(imageInfoPath);
        ImageArtifactDetails? imageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(imageInfoJson);
        return imageArtifactDetails is null
            ? throw new JsonException($"Unable to correctly deserialize path '{imageInfoJson}'.")
            : imageArtifactDetails;
    }
}
