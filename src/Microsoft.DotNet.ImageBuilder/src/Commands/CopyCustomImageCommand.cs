// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;

namespace Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable
[Export(typeof(ICommand))]
public class CopyCustomImageCommand : Command<CopyCustomImageOptions, CopyCustomImageOptionsBuilder>
{
    private readonly ICopyImageService _copyImageService;

    [ImportingConstructor]
    public CopyCustomImageCommand(ICopyImageService copyImageService)
    {
        _copyImageService = copyImageService;
    }

    protected override string Description => "Imports the specified image from one registry to another";

    public override async Task ExecuteAsync()
    {
        string srcRegistry = DockerHelper.GetRegistry(Options.ImageName) ?? DockerHelper.DockerHubRegistry;
        string srcImage = DockerHelper.TrimRegistry(Options.ImageName, srcRegistry);

        ContainerRegistryImportSourceCredentials? importSourceCreds = null;
        if (Options.CredentialsOptions.Credentials.TryGetValue(srcRegistry, out RegistryCredentials? registryCreds))
        {
            importSourceCreds = new ContainerRegistryImportSourceCredentials(registryCreds.Password)
            {
                Username = registryCreds.Username
            };
        }

        await _copyImageService.ImportImageAsync(
            Options.Subscription,
            Options.ResourceGroup,
            [srcImage],
            Options.DestinationRegistry,
            Options.ImageName,
            srcRegistry,
            sourceCredentials: importSourceCreds,
            isDryRun: Options.IsDryRun);
    }
}
