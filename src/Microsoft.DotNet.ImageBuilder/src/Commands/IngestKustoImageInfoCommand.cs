// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class IngestKustoImageInfoCommand : ManifestCommand<IngestKustoImageInfoOptions>
    {
        private readonly IKustoClient kustoClient;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public IngestKustoImageInfoCommand(ILoggerService loggerService, IKustoClient kustoClient)
        {
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.kustoClient = kustoClient ?? throw new ArgumentNullException(nameof(kustoClient));
        }

        public override async Task ExecuteAsync()
        {
            this.loggerService.WriteHeading("INGESTING IMAGE INFO DATA INTO KUSTO");

            string csv = GetImageInfoCsv();
            this.loggerService.WriteMessage($"Image Info to Ingest:{Environment.NewLine}{csv}");

            using MemoryStream stream = new MemoryStream();
            using StreamWriter writer = new StreamWriter(stream);
            writer.Write(csv);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            if (!Options.IsDryRun)
            {
                await kustoClient.IngestFromCsvStreamAsync(stream, Options);
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
                        builder.AppendLine(FormatCsv(platform.Digest, platform, image, repo, timestamp));

                        foreach (string tag in platform.SimpleTags)
                        {
                            builder.AppendLine(FormatCsv(tag, platform, image, repo, timestamp));
                        }
                    }
                }
            }

            // Kusto ingest API does not handle an empty line, therefore the last line must be trimmed.
            return builder.ToString().TrimEnd(Environment.NewLine);
        }

        private string FormatCsv(string imageId, PlatformData platform, ImageData image, RepoData repo, string timestamp) =>
            $"\"{imageId}\",\"{platform.Architecture}\",\"{platform.OsType}\",\"{platform.OsVersion}\","
                + $"\"{image.ProductVersion}\",\"{platform.Dockerfile}\",\"{repo.Repo}\",\"{timestamp}\"";
    }
}
