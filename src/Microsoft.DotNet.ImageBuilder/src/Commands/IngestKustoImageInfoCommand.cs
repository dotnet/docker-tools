// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valleysoft.DockerfileModel;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class IngestKustoImageInfoCommand : ManifestCommand<IngestKustoImageInfoOptions, IngestKustoImageInfoOptionsBuilder>
    {
        private readonly IKustoClient _kustoClient;
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public IngestKustoImageInfoCommand(ILoggerService loggerService, IKustoClient kustoClient)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _kustoClient = kustoClient ?? throw new ArgumentNullException(nameof(kustoClient));
        }

        protected override string Description => "Ingests image info data into Kusto";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("INGESTING IMAGE INFO DATA INTO KUSTO");

            (string imageInfo, string layerInfo) = GetImageInfoCsv();
            _loggerService.WriteMessage($"Image Info to Ingest:{Environment.NewLine}{imageInfo}{Environment.NewLine}");
            _loggerService.WriteMessage($"Image Layer to Ingest:{Environment.NewLine}{layerInfo}{Environment.NewLine}");

            if (string.IsNullOrEmpty(imageInfo))
            {
                if (!string.IsNullOrEmpty(layerInfo))
                {
                    throw new InvalidOperationException("Unexpected layer info when image info is empty.");
                }

                _loggerService.WriteMessage("Skipping ingestion due to empty image info data.");
                return;
            }

            await IngestInfoAsync(imageInfo, Options.ImageTable);
            await IngestInfoAsync(layerInfo, Options.LayerTable);
        }

        private (string imageInfo, string layerInfo) GetImageInfoCsv()
        {
            StringBuilder imageInfo = new();
            StringBuilder layerInfo = new();

            foreach (RepoData repo in ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest).Repos)
            {
                foreach (ImageData image in repo.Images)
                {
                    foreach (PlatformData platform in image.Platforms)
                    {
                        string timestamp = platform.Created.ToUniversalTime().ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss");
                        string sha = ImageName.Parse(platform.Digest).Digest!;
                        imageInfo.AppendLine(FormatImageCsv(sha, platform, image, repo.Repo, timestamp));

                        IEnumerable<TagInfo> tagInfos = platform.PlatformInfo.Tags
                            .Where(tagInfo => platform.SimpleTags.Contains(tagInfo.Name))
                            .ToList();

                        IEnumerable<string> syndicatedRepos = tagInfos
                            .Select(tag => tag.SyndicatedRepo)
                            .Where(repo => repo != null)
                            .Distinct();

                        foreach (string syndicatedRepo in syndicatedRepos)
                        {
                            imageInfo.AppendLine(
                                FormatImageCsv(sha, platform, image, syndicatedRepo, timestamp));
                        }

                        foreach (TagInfo tag in tagInfos)
                        {
                            imageInfo.AppendLine(FormatImageCsv(tag.Name, platform, image, repo.Repo, timestamp));

                            if (tag.SyndicatedRepo != null)
                            {
                                foreach (string destinationTag in tag.SyndicatedDestinationTags)
                                {
                                    imageInfo.AppendLine(
                                       FormatImageCsv(destinationTag, platform, image, tag.SyndicatedRepo, timestamp));
                                }
                            }
                        }

                        for (int i = 0; i < platform.Layers.Count; i++)
                        {
                            // TODO: Track layer size (currently set to 0) https://github.com/dotnet/docker-tools/issues/745
                            layerInfo.AppendLine(FormatLayerCsv(
                                platform.Layers[i], 0, platform.Layers.Count - i, sha, platform, image, repo.Repo, timestamp));
                        }
                    }
                }
            }

            // Kusto ingest API does not handle an empty line, therefore the last line must be trimmed.
            return (imageInfo.ToString().TrimEnd(Environment.NewLine), layerInfo.ToString().TrimEnd(Environment.NewLine));
        }

        private static string FormatImageCsv(string imageId, PlatformData platform, ImageData image, string repo, string timestamp) =>
            $"\"{imageId}\",\"{platform.Architecture}\",\"{platform.OsType}\",\"{platform.PlatformInfo.GetOSDisplayName()}\","
                + $"\"{image.ProductVersion}\",\"{platform.Dockerfile}\",\"{repo}\",\"{timestamp}\"";

        private static string FormatLayerCsv(
            string layerDigest,
            int size,
            int ordinal,
            string imageDigest,
            PlatformData platform,
            ImageData image,
            string repo,
            string timestamp) =>
                $"\"{layerDigest}\",\"{size}\",\"{ordinal}\",{FormatImageCsv(imageDigest, platform, image, repo, timestamp)}";

        private async Task IngestInfoAsync(string info, string table)
        {
            using MemoryStream stream = new();
            using StreamWriter writer = new(stream);
            writer.Write(info);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            if (!Options.IsDryRun)
            {
                await _kustoClient.IngestFromCsvStreamAsync(stream, Options.ServicePrincipal, Options.Cluster, Options.Database, table, Options.IsDryRun);
            }
        }
    }
}
#nullable disable
