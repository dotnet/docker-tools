// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Services;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyDockerHubBaseImagesCommand : CopyImagesCommand<CopyImagesOptions, CopyImagesOptionsBuilder>
    {
        [ImportingConstructor]
        public CopyDockerHubBaseImagesCommand(
            IAzureManagementFactory azureManagementFactory, ILoggerService loggerService)
            : base(azureManagementFactory, loggerService)
        {
        }

        protected override string Description => "Copies external base images from Docker Hub to ACR";

        protected override async Task ExecuteCoreAsync()
        {
            LoggerService.WriteHeading("COPYING IMAGES");

            IEnumerable<Task> importTasks = Manifest.GetExternalFromImages()
                .Where(fromImage => !fromImage.StartsWith(Manifest.Registry))
                .Select(fromImage => ImportImageAsync($"{Options.RepoPrefix}{fromImage}", fromImage, srcRegistryName: "docker.io"));

            await Task.WhenAll(importTasks);
        }
    }
}
