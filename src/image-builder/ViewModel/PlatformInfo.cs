using ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ImageBuilder.ViewModel
{
    public class PlatformInfo
    {
        private static Regex FromRegex { get; } = new Regex(@"FROM\s+(?<fromImage>\S+)");

        public string FromImage { get; private set; }
        public bool IsExternalFromImage { get; private set; }
        public Platform Model { get; private set; }
        public IEnumerable<string> Tags { get; private set; }

        public static PlatformInfo Create(Platform model, Repo repo)
        {
            PlatformInfo platformInfo = new PlatformInfo();
            platformInfo.Model = model;
            platformInfo.InitializeFromImage();
            platformInfo.IsExternalFromImage = !platformInfo.FromImage.StartsWith($"{repo.DockerRepo}:");
            platformInfo.Tags = model.Tags
                .Select(tag => $"{repo.DockerRepo}:{tag}")
                .ToArray();

            return platformInfo;
        }

        private void InitializeFromImage()
        {
            Match fromMatch = FromRegex.Match(File.ReadAllText(Path.Combine(Model.Dockerfile, "Dockerfile")));
            if (!fromMatch.Success)
            {
                throw new InvalidOperationException($"Unable to find the FROM image in {Model.Dockerfile}.");
            }

            FromImage = fromMatch.Groups["fromImage"].Value;
        }

        public override string ToString()
        {
            return
$@"Dockerfile Path:  {Model.Dockerfile}
FromImage:  {FromImage}
IsExternalFromImage:  {IsExternalFromImage}
Tags:
  {string.Join($"{Environment.NewLine}  ", Tags)}";
        }
    }
}
