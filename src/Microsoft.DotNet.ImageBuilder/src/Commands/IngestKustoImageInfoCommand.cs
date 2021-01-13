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
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class IngestKustoImageInfoCommand : ManifestCommand<IngestKustoImageInfoOptions, IngestKustoImageInfoSymbolsBuilder>
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

            string csv = GetImageInfoCsv();
            _loggerService.WriteMessage($"Image Info to Ingest:{Environment.NewLine}{csv}");

            if (string.IsNullOrEmpty(csv))
            {
                _loggerService.WriteMessage("Skipping ingestion due to empty image info data.");
                return;
            }

            using MemoryStream stream = new MemoryStream();
            using StreamWriter writer = new StreamWriter(stream);
            writer.Write(csv);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            if (!Options.IsDryRun)
            {
                await _kustoClient.IngestFromCsvStreamAsync(stream, Options);
            }
        }

        private string GetImageInfoCsv()
        {
            StringBuilder builder = new StringBuilder();

            foreach (RepoData repo in ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest).Repos)
            {
                foreach (ImageData image in repo.Images)
                {
                    foreach (PlatformData platform in image.Platforms)
                    {
                        string timestamp = platform.Created.ToUniversalTime().ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss");
                        string sha = DockerHelper.GetDigestSha(platform.Digest);
                        builder.AppendLine(FormatCsv(sha, platform, image, repo.Repo, timestamp));

                        IEnumerable<TagInfo> tagInfos = platform.PlatformInfo.Tags
                            .Where(tagInfo => platform.SimpleTags.Contains(tagInfo.Name))
                            .ToList();

                        IEnumerable<string> syndicatedRepos = tagInfos
                            .Select(tag => tag.SyndicatedRepo)
                            .Where(repo => repo != null)
                            .Distinct();

                        foreach (string syndicatedRepo in syndicatedRepos)
                        {
                            builder.AppendLine(
                                FormatCsv(sha, platform, image, syndicatedRepo, timestamp));
                        }

                        foreach (TagInfo tag in tagInfos)
                        {
                            builder.AppendLine(FormatCsv(tag.Name, platform, image, repo.Repo, timestamp));

                            if (tag.SyndicatedRepo != null)
                            {
                                foreach (string destinationTag in tag.SyndicatedDestinationTags)
                                {
                                    builder.AppendLine(
                                       FormatCsv(destinationTag, platform, image, tag.SyndicatedRepo, timestamp));
                                }
                            }
                        }
                    }
                }
            }

            // Kusto ingest API does not handle an empty line, therefore the last line must be trimmed.
            return builder.ToString().TrimEnd(Environment.NewLine);
        }

        private string FormatCsv(string imageId, PlatformData platform, ImageData image, string repo, string timestamp) =>
            $"\"{imageId}\",\"{platform.Architecture}\",\"{platform.OsType}\",\"{platform.PlatformInfo.GetOSDisplayName()}\","
                + $"\"{image.ProductVersion}\",\"{platform.Dockerfile}\",\"{repo}\",\"{timestamp}\"";
    }
}
