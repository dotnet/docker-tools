using ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageBuilder.ViewModel
{
    public class ImageInfo
    {
        public PlatformInfo Platform { get; private set; }
        public IEnumerable<string> Tags { get; private set; }
        public Image Model { get; set; }

        public static ImageInfo Create(Image model, string dockerOS, Repo repo)
        {
            ImageInfo imageInfo = new ImageInfo();
            imageInfo.Model = model;
            imageInfo.Tags = model.SharedTags.Select(tag => $"{repo.DockerRepo}:{tag}");

            if (model.Platforms.TryGetValue(dockerOS, out Platform platform))
            {
                imageInfo.Platform = PlatformInfo.Create(platform, repo);
                imageInfo.Tags = imageInfo.Tags.Concat(imageInfo.Platform.Tags);
            }

            imageInfo.Tags = imageInfo.Tags.ToArray();

            return imageInfo;
        }

        public override string ToString()
        {
            return
$@"Tags:
  {string.Join($"{Environment.NewLine}  ", Tags)}
Platform (
{Platform?.ToString()}
)";
        }
    }
}
