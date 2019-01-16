// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyAcrImagesCommand : Command<CopyAcrImagesOptions>
    {
        public CopyAcrImagesCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("COPING IMAGES");

            using (AzureHelper helper = AzureHelper.Create(Options.Username, Options.Password, Options.Tenant, Options.IsDryRun))
            {
                IEnumerable<TagInfo> platformTags = Manifest.ActiveImages
                    .SelectMany(image => image.ActivePlatforms)
                    .SelectMany(platform => platform.Tags);
                string fullRegistryName = $"{Options.Registry}.azurecr.io/";

                foreach (TagInfo platformTag in platformTags)
                {
                    string sourceImage = $"{Options.SourceRepository}:{platformTag.Name}";
                    string destImage = platformTag.FullyQualifiedName.TrimStart(fullRegistryName);
                    helper.ExecuteAzCommand(
                        $"acr import -n {Options.Registry} --source {sourceImage} -t {destImage} --force",
                        Options.IsDryRun);
                }
            };

            return Task.CompletedTask;
        }
    }
}
