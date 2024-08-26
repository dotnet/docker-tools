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
    private readonly IContainerRegistryContentClientFactory _acrContentClientFactory;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
    private readonly IOrasService _orasService;
    private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
    private readonly DateOnly _eolDate;

    [ImportingConstructor]
    public GenerateEolAnnotationDataCommand(
        IDotNetReleasesService dotNetReleasesService,
        ILoggerService loggerService,
        IContainerRegistryClientFactory acrClientFactory,
        IContainerRegistryContentClientFactory acrContentClientFactory,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        IRegistryCredentialsProvider registryCredentialsProvider,
        IOrasService orasService)
    {
        _dotNetReleasesService = dotNetReleasesService ?? throw new ArgumentNullException(nameof(dotNetReleasesService));
        _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        _acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
        _acrContentClientFactory = acrContentClientFactory ?? throw new ArgumentNullException(nameof(acrContentClientFactory));
        _tokenCredentialProvider = tokenCredentialProvider ?? throw new ArgumentNullException(nameof(tokenCredentialProvider));
        _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
        _orasService = orasService ?? throw new ArgumentNullException(nameof(orasService));

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
            Options.RegistryName,
            Options.RegistryName);

    private void WriteDigestDataJson(List<EolDigestData> digestsToAnnotate)
    {
        EolAnnotationsData eolAnnotations = new(digestsToAnnotate, _eolDate);

        string annotationsJson = JsonConvert.SerializeObject(
            eolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        File.WriteAllText(Options.EolDigestsListPath, annotationsJson);
    }

    private async Task<List<EolDigestData>> GetDigestsToAnnotate()
    {
        Dictionary<string, DateOnly> productEolDates = await _dotNetReleasesService.GetProductEolDatesFromReleasesJson();
        ImageArtifactDetails oldImageArtifactDetails = LoadImageInfoData(Options.OldImageInfoPath);
        ImageArtifactDetails newImageArtifactDetails = LoadImageInfoData(Options.NewImageInfoPath);

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
                .Select(name => Options.RepoPrefix + name);
            IEnumerable<(string Digest, string? Tag)> registryDigests = await GetAllImageDigestsFromRegistry(repoNames);

            IEnumerable<string> supportedDigests = GetSupportedDigests(newImageArtifactDetails);
            IEnumerable<EolDigestData> unsupportedDigests = GetUnsupportedDigests(registryDigests, supportedDigests);

            // Annotate digests that are not already annotated for EOL
            ConcurrentBag<EolDigestData> digetsToAnnotate = [];
            Parallel.ForEach(unsupportedDigests, digest =>
            {
                if (!_orasService.IsDigestAnnotatedForEol(digest.Digest, _loggerService, Options.IsDryRun, out _))
                {
                    digetsToAnnotate.Add(digest);
                }
            });

            digestDataList.AddRange(digetsToAnnotate);

            if (Options.AnnotateEolProducts)
            {
                // Annotate images for eol products in new image info
                foreach (ImageData image in newImageArtifactDetails.Repos.SelectMany(repo => repo.Images))
                {
                    digestDataList.AddRange(GetProductEolDigests(image, productEolDates));
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
    private static IEnumerable<EolDigestData> GetUnsupportedDigests(IEnumerable<(string Digest, string? Tag)> registryDigests, IEnumerable<string> supportedDigests) =>
        registryDigests
            .Where(registryDigest => !supportedDigests.Contains(registryDigest.Digest))
            .Select(registryDigest => new EolDigestData(registryDigest.Digest) { Tag = registryDigest.Tag });

    private IEnumerable<string> GetSupportedDigests(ImageArtifactDetails newImageArtifactDetails) =>
        newImageArtifactDetails.Repos
            .SelectMany(repo => repo.Images)
            .SelectMany(GetImageDigests)
            .Select(digest => digest.Digest);
    
    private IEnumerable<(string Digest, string? Tag)> GetImageDigests(ImageData image)
    {
        if (image.Manifest is not null)
        {
            yield return (ReplaceMcrWithAcr(image.Manifest.Digest), GetLongestTag(image.Manifest.SharedTags));
        }

        foreach (PlatformData platform in image.Platforms)
        {
            yield return (ReplaceMcrWithAcr(platform.Digest), GetLongestTag(platform.SimpleTags));
        }
    }

    // This is used for transforming the image names in the image info file to match the image names in the ACR
    private string ReplaceMcrWithAcr(string imageName) =>
        imageName.Replace("mcr.microsoft.com/", $"{Options.RegistryName}/{Options.RepoPrefix}");

    private static string? GetLongestTag(IEnumerable<string> tags) =>
        tags.OrderByDescending(tag => tag.Length).FirstOrDefault();

    private async Task<IEnumerable<(string Digest, string? Tag)>> GetAllImageDigestsFromRegistry(IEnumerable<string> repoNames)
    {
        IContainerRegistryClient acrClient = _acrClientFactory.Create(Options.RegistryName, _tokenCredentialProvider.GetCredential());
        IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNamesAsync();

        ConcurrentBag<(string Digest, string? Tag)> digests = [];
        await foreach (string repositoryName in repositoryNames.Where(name => repoNames.Contains(name)))
        {
            IContainerRegistryContentClient contentClient =
                _acrContentClientFactory.Create(Options.RegistryName, repositoryName, _tokenCredentialProvider.GetCredential());

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
                    string imageName = DockerHelper.GetImageName(Options.RegistryName, repositoryName, digest: manifestProps.Digest);
                    digests.Add((imageName, GetLongestTag(manifestProps.Tags)));
                }
            }
        }

        return digests;
    }

    private IEnumerable<EolDigestData> GetProductEolDigests(ImageData image, Dictionary<string, DateOnly> productEolDates)
    {
        if (image.ProductVersion == null)
        {
            return [];
        }

        // Check if the version has a pre-release label. If so, it's not EOL by definition.
        if (image.ProductVersion.Contains("-"))
        {
            return [];
        }

        string dotnetVersion = Version.Parse(image.ProductVersion).ToString(2);
        if (!productEolDates.TryGetValue(dotnetVersion, out DateOnly date))
        {
            return [];
        }

        return GetImageDigests(image).Select(val => new EolDigestData(val.Digest) { Tag = val.Tag, EolDate = date });
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
