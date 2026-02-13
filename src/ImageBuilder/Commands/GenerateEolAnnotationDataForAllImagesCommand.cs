// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateEolAnnotationDataForAllImagesCommand :
    GenerateEolAnnotationDataCommandBase<GenerateEolAnnotationDataOptions, GenerateEolAnnotationDataOptionsBuilder>
{
    public GenerateEolAnnotationDataForAllImagesCommand(
        ILogger loggerService,
        IAcrClientFactory acrClientFactory,
        IAcrContentClientFactory acrContentClientFactory,
        ILifecycleMetadataService lifecycleMetadataService,
        IRegistryCredentialsProvider registryCredentialsProvider)
        : base(
            loggerService,
            acrContentClientFactory,
            acrClientFactory,
            lifecycleMetadataService,
            registryCredentialsProvider)
    {
    }

    protected override string Description => "Generate EOL annotation data for all images in the registry";

    protected override async Task<IEnumerable<EolDigestData>> GetDigestsToAnnotateAsync() =>
        // All images in all repos of the registry are marked as unsupported.
        await GetAllImageDigestsFromRegistryAsync();
}
