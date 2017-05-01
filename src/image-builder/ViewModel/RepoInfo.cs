using ImageBuilder.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ImageBuilder.ViewModel
{
    public class RepoInfo
    {
        private string DockerOS { get; set; }
        public IEnumerable<ImageInfo> Images { get; set; }
        public Repo Model { get; set; }

        public string[] TestCommands
        {
            get
            {
                Model.TestCommands.TryGetValue(DockerOS, out string[] commands);
                return commands;
            }
        }

        private RepoInfo()
        {
        }

        public static RepoInfo Create(string repoJsonPath)
        {
            RepoInfo repoInfo = new RepoInfo();
            repoInfo.InitializeDockerOS();
            string json = File.ReadAllText(repoJsonPath);
            repoInfo.Model = JsonConvert.DeserializeObject<Repo>(json);
            repoInfo.Images = repoInfo.Model.Images
                .Select(image => ImageInfo.Create(image, repoInfo.DockerOS, repoInfo.Model))
                .ToArray();

            return repoInfo;
        }

        public IEnumerable<string> GetPlatformTags()
        {
            return Images
                .Where(image => image.Platform != null)
                .SelectMany(image => image.Platform.Tags);
        }

        private void InitializeDockerOS()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("docker", "version -f \"{{ .Server.Os }}\"");
            startInfo.RedirectStandardOutput = true;
            Process process = ExecuteHelper.Execute(startInfo, false, $"Failed to detect Docker Mode");
            DockerOS = process.StandardOutput.ReadToEnd().Trim();
        }

        public override string ToString()
        {
            string images = Images
                .Select(image => image.ToString())
                .Aggregate((working, next) => $"{working}{Environment.NewLine}----------{Environment.NewLine}{next}");

            return
$@"DockerOS:  {DockerOS}
DockerRepo:  {Model.DockerRepo}
TestCommands:
{string.Join(Environment.NewLine, TestCommands)}
Images [
{images}
]";
        }
    }
}
