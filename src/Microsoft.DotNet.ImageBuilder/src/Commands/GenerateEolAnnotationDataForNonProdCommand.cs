// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

[Export(typeof(ICommand))]
public class GenerateEolAnnotationDataForNonProdCommand :
    GenerateEolAnnotationDataCommandBase<GenerateEolAnnotationDataOptions, GenerateEolAnnotationDataOptionsBuilder>
{
    [ImportingConstructor]
    public GenerateEolAnnotationDataForNonProdCommand(
        ILoggerService loggerService,
        IContainerRegistryClientFactory acrClientFactory,
        IContainerRegistryContentClientFactory acrContentClientFactory,
        IAzureTokenCredentialProvider tokenCredentialProvider,
        IRegistryCredentialsProvider registryCredentialsProvider,
        ILifecycleMetadataService lifecycleMetadataService)
        : base(
            loggerService,
            tokenCredentialProvider,
            acrContentClientFactory,
            acrClientFactory,
            lifecycleMetadataService)
    {
    }

    protected override string Description => "Generate EOL annotation data for non-prod images";

    public override async Task ExecuteAsync()
    {
        // All images in all repos of the registry are marked as unsupported.
        Dictionary<string, string?> registryTagsByDigest = await GetAllImageDigestsFromRegistryAsync();
        IEnumerable<EolDigestData> eolDigests = GetUnsupportedDigests(registryTagsByDigest, []);
        WriteDigestDataJson(eolDigests);
    }
}
