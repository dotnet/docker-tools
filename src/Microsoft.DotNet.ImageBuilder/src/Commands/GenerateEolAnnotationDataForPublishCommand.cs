// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

[Export(typeof(ICommand))]
public class GenerateEolAnnotationDataForPublishCommand :
    GenerateEolAnnotationDataCommandBase<GenerateEolAnnotationDataForPublishOptions, GenerateEolAnnotationDataOptionsForPublishBuilder>
{
    [ImportingConstructor]
    public GenerateEolAnnotationDataForPublishCommand(
        ILoggerService loggerService,
        IContainerRegistryClientFactory acrClientFactory,
        IContainerRegistryContentClientFactory acrContentClientFactory,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        ILifecycleMetadataService lifecycleMetadataService,
        IRegistryCredentialsProvider registryCredentialsProvider)
        : base(
            loggerService,
            tokenCredentialProvider,
            acrContentClientFactory,
            acrClientFactory,
            lifecycleMetadataService,
            registryCredentialsProvider)
    {
    }

    protected override string Description => "Generate EOL annotation data for all images not described in the new image info file";

    protected override async Task<IEnumerable<EolDigestData>> GetDigestsToAnnotateAsync()
    {
        if (!File.Exists(Options.OldImageInfoPath) && !File.Exists(Options.NewImageInfoPath))
        {
            LoggerService.WriteMessage("No digests to annotate because no image info files were provided.");
            return [];
        }

        ImageArtifactDetails oldImageArtifactDetails = ImageInfoHelper.DeserializeImageArtifactDetails(Options.OldImageInfoPath);
        ImageArtifactDetails newImageArtifactDetails = ImageInfoHelper.DeserializeImageArtifactDetails(Options.NewImageInfoPath);

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
            Dictionary<string, string?> registryTagsByDigest =
                await GetAllImageDigestsFromRegistryAsync(repo => repoNames.Contains(repo));

            if (!Options.IsDryRun)
            {
                // Only apply the registry override if it's not a dry run. This is because it relies on the input image info file
                // to have populated digest values but that won't be the case in a dry run.
                newImageArtifactDetails = newImageArtifactDetails.ApplyRegistryOverride(Options.RegistryOptions);
            }

            IEnumerable<string> supportedDigests = newImageArtifactDetails.GetAllDigests();

            IEnumerable<EolDigestData> unsupportedDigests = GetUnsupportedDigests(registryTagsByDigest, supportedDigests);
            return GetDigestsWithoutExistingAnnotation(unsupportedDigests);
        }
        catch (Exception e)
        {
            LoggerService.WriteError($"Error occurred while generating EOL annotation data: {e}");
            throw;
        }
    }
}
