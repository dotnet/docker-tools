// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.DotNet.ImageBuilder.Services;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyBaseImagesCommand : CopyImagesCommand<CopyBaseImagesOptions, CopyBaseImagesOptionsBuilder>
    {
        [ImportingConstructor]
        public CopyBaseImagesCommand(
            IAzureManagementFactory azureManagementFactory, ILoggerService loggerService)
            : base(azureManagementFactory, loggerService)
        {
        }

        protected override string Description => "Copies external base images from their source registry to ACR";

        public override async Task ExecuteAsync()
        {
            LoggerService.WriteHeading("COPYING IMAGES");

            IEnumerable<Task> importTasks = Manifest.GetExternalFromImages()
                .Where(fromImage => !fromImage.StartsWith(Manifest.Registry))
                .Select(fromImage =>
                {
                    string registry = DockerHelper.GetRegistry(fromImage) ?? "docker.io";
                    fromImage = DockerHelper.TrimRegistry(fromImage, registry);

                    Options.SourceCredentials.TryGetValue(registry, out ImportSourceCredentials? sourceCreds);

                    return ImportImageAsync($"{Options.RepoPrefix}{fromImage}", fromImage, srcRegistryName: registry,
                        sourceCredentials: sourceCreds);
                });

            await Task.WhenAll(importTasks);
        }
    }
}
#nullable disable
