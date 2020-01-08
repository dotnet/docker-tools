// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class ValidateImageSizeCommand : ImageSizeCommand<ValidateImageSizeOptions>
    {
        private readonly ILoggerService loggerService;
        private readonly IEnvironmentService environmentService;

        [ImportingConstructor]
        public ValidateImageSizeCommand(IDockerService dockerService, ILoggerService loggerService, IEnvironmentService environmentService)
            : base(dockerService)
        {
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        public ImageSizeValidationResults ValidationResults { get; private set; }

        public override Task ExecuteAsync()
        {
            ValidationResults = ValidateImages();
            LogResults(ValidationResults);
            return Task.CompletedTask;
        }

        private ImageSizeValidationResults ValidateImages()
        {
            loggerService.WriteHeading("VALIDATING IMAGE SIZES");

            Dictionary<string, ImageSizeInfo> imageData = LoadBaseline();

            // This handler will be invoked for each image defined in the manifest
            void processImage(string repoId, string imageId, string tagName)
            {
                // If the CheckBaselineIntegrityOnly option is enabled, we want to skip the retrieval
                // of the image size.
                long? currentSize = null;
                if (!Options.CheckBaselineIntegrityOnly)
                {
                    currentSize = GetImageSize(tagName);
                }
                
                // If the image is found in the generated set of ImageSizeInfos, it means we have
                // baseline data for it and just need to update its CurrentSize.
                if (imageData.TryGetValue(imageId, out ImageSizeInfo imageSizeInfo))
                {
                    imageSizeInfo.ImageExistsOnDisk = true;
                    imageSizeInfo.CurrentSize = currentSize;
                }
                // Else the image has no baseline data defined, so we'll add a new entry w/o baseline size.
                else
                {
                    imageData.Add(imageId, new ImageSizeInfo
                    {
                        Id = imageId,
                        CurrentSize = currentSize,
                        ImageExistsOnDisk = true
                    });
                }
            }

            ProcessImages(processImage);
            return ValidateImages(imageData.Values);
        }

        private ImageSizeValidationResults ValidateImages(IEnumerable<ImageSizeInfo> imageSizeInfos)
        {
            IEnumerable<ImageSizeInfo> baselinedImages = imageSizeInfos.Where(info => info.BaselineSize.HasValue).ToArray();

            IEnumerable<ImageSizeInfo> imagesWithMissingBaseline = imageSizeInfos.Except(baselinedImages).ToArray();
            IEnumerable<ImageSizeInfo> imagesWithExtraneousBaseline = imageSizeInfos.Where(info => !info.ImageExistsOnDisk).ToArray();
            IEnumerable<ImageSizeInfo> missingOrExtraImages = imagesWithMissingBaseline.Concat(imagesWithExtraneousBaseline).ToArray();

            IEnumerable<ImageSizeInfo> imagesWithNoSizeChange;
            IEnumerable<ImageSizeInfo> imagesWithAllowedSizeChange;
            IEnumerable<ImageSizeInfo> imagesWithDisallowedSizeChange;
            if (Options.CheckBaselineIntegrityOnly)
            {
                imagesWithNoSizeChange =
                    imagesWithAllowedSizeChange =
                    imagesWithDisallowedSizeChange =
                    Enumerable.Empty<ImageSizeInfo>();
            }
            else
            {
                imagesWithNoSizeChange = baselinedImages.Where(info => info.SizeDifference == 0).ToArray();
                imagesWithAllowedSizeChange = baselinedImages
                    .Except(missingOrExtraImages)
                    .Where(info => info.SizeDifference != 0 && info.WithinAllowedVariance).ToArray();
                imagesWithDisallowedSizeChange = baselinedImages
                    .Except(missingOrExtraImages)
                    .Where(info => !info.WithinAllowedVariance).ToArray();
            }

            return new ImageSizeValidationResults(
                imagesWithNoSizeChange,
                imagesWithAllowedSizeChange,
                imagesWithDisallowedSizeChange,
                imagesWithMissingBaseline,
                imagesWithExtraneousBaseline);
        }

        private void LogResults(ImageSizeValidationResults results)
        {
            loggerService.WriteHeading("VALIDATION RESULTS");
            LogResults(results.ImagesWithNoSizeChange, "Images with no size change:");
            LogResults(results.ImagesWithAllowedSizeChange, "Images with allowed size change:");
            LogResults(results.ImagesWithDisallowedSizeChange, "Images exceeding size variance:");
            LogResults(results.ImagesWithMissingBaseline, "Images missing from baseline:");
            LogResults(results.ImagesWithExtraneousBaseline, "Extra baseline images not defined in manifest:");

            if (results.ImagesWithDisallowedSizeChange.Any() ||
                results.ImagesWithMissingBaseline.Any() ||
                results.ImagesWithExtraneousBaseline.Any())
            {
                loggerService.WriteError("Image size validation failed");
                loggerService.WriteMessage("The baseline file can be updated by running the updateImageSizeBaseline command.");

                this.environmentService.Exit(1);
            }
        }

        private void LogResults(IEnumerable<ImageSizeInfo> imageData, string header)
        {
            if (imageData.Any())
            {
                loggerService.WriteSubheading(header);

                string indent = new string(' ', 4);

                foreach (ImageSizeInfo info in imageData)
                {
                    StringBuilder msg = new StringBuilder();
                    msg.AppendLine(info.Id);

                    if (info.CurrentSize.HasValue)
                    {
                        msg.AppendLine($"{indent}Actual:     {info.CurrentSize,15:N0}");
                    }

                    if (info.BaselineSize.HasValue)
                    {
                        msg.AppendLine($"{indent}Expected:   {info.BaselineSize,15:N0}");
                    }

                    if (info.SizeDifference.HasValue)
                    {
                        msg.AppendLine($"{indent}Difference: {info.SizeDifference,15:N0}");
                    }

                    if (info.MinVariance.HasValue)
                    {
                        msg.AppendLine($"{indent}Variation Allowed: {info.MinVariance:N0} - {info.MaxVariance:N0}");
                    }

                    loggerService.WriteMessage(msg.ToString());
                }

                loggerService.WriteMessage("----------------------------------------------------");
            }
        }
    }
}
