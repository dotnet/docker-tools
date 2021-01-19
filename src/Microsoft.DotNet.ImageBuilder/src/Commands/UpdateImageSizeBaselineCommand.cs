// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class UpdateImageSizeBaselineCommand : ImageSizeCommand<UpdateImageSizeBaselineOptions, UpdateImageSizeBaselineOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public UpdateImageSizeBaselineCommand(IDockerService dockerService, ILoggerService loggerService)
            : base(dockerService)
        {
            _loggerService = loggerService;
        }

        protected override string Description => "Updates an image size baseline file with current image sizes";

        public override Task ExecuteAsync()
        {
            UpdateBaseline();
            return Task.CompletedTask;
        }

        private void UpdateBaseline()
        {
            _loggerService.WriteHeading("UPDATING IMAGE SIZE BASELINE");

            Dictionary<string, ImageSizeInfo> imageData = null;
            if (!Options.AllBaselineData)
            {
                imageData = LoadBaseline();
            }

            JObject json = new JObject();

            void processImage(string repoId, string imageId, string imageTag)
            {
                _loggerService.WriteMessage($"Processing '{imageId}'");

                long imageSize = GetImageSize(imageTag);

                if (!Options.AllBaselineData && imageData.TryGetValue(imageId, out ImageSizeInfo imageSizeInfo))
                {
                    imageSizeInfo.CurrentSize = imageSize;

                    if (imageSizeInfo.WithinAllowedVariance)
                    {
                        _loggerService.WriteMessage(
                            $"Skipping '{imageId}' because its image size ({imageSize}) is within the allowed range ({imageSizeInfo.MinVariance}-{imageSizeInfo.MaxVariance})");
                        imageSize = imageSizeInfo.BaselineSize.Value;
                    }
                }

                JObject repo;
                if (json.TryGetValue(repoId, out JToken repoToken))
                {
                    repo = (JObject)repoToken;
                }
                else
                {
                    json[repoId] = repo = new JObject();
                }

                repo.Add(imageId, new JValue(imageSize));
            }

            _loggerService.WriteSubheading($"Processing images");
            ProcessImages(processImage);

            _loggerService.WriteSubheading($"Updating `{Options.BaselinePath}`");
            string formattedJson = json.ToString();
            if (File.Exists(Options.BaselinePath))
            {
                formattedJson = formattedJson.NormalizeLineEndings(File.ReadAllText(Options.BaselinePath));
            }

            _loggerService.WriteMessage(formattedJson);
            File.WriteAllText(Options.BaselinePath, formattedJson);
        }
    }
}
