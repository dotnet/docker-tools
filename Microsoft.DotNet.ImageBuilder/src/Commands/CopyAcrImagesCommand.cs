// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                string registryName = $"{Manifest.Registry}/";

                foreach (TagInfo platformTag in Manifest.GetFilteredPlatformTags())
                {
                    string sourceImage = platformTag.FullyQualifiedName.Replace(Options.RepoPrefix, Options.SourceRepoPrefix);
                    string destImage = platformTag.FullyQualifiedName.TrimStart(registryName);
                    helper.ExecuteAzCommand(
                        $"acr import -n {Manifest.Registry.TrimEnd(".azurecr.io")} --source {sourceImage} -t {destImage} --force",
                        Options.IsDryRun);
                }
            };

            return Task.CompletedTask;
        }
    }
}
