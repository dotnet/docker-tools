using ImageBuilder.Model;
using ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ImageBuilder
{
    public static class ImageBuilder
    {
        private static Options Options { get; set; }
        private static RepoInfo RepoInfo { get; set; }

        public static int Main(string[] args)
        {
            int result = 0;

            try
            {
                Options = Options.ParseArgs(args);
                if (Options.IsHelpRequest)
                {
                    Console.WriteLine(Options.Usage);
                }
                else
                {
                    InitializeRepoInfo();

                    switch (Options.Command)
                    {
                        case CommandType.Build:
                            ExecuteBuild();
                            break;
                        case CommandType.Manifest:
                            ExecuteManifest();
                            break;
                    };
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                result = 1;
            }

            return result;
        }

        private static void BuildImages()
        {
            WriteHeading("BUILDING IMAGES");
            foreach (ImageInfo imageInfo in RepoInfo.Images.Where(image => image.Platform != null))
            {
                Console.WriteLine($"-- BUILDING: {imageInfo.Platform.Model.Dockerfile}");
                if (!Options.IsSkipPullingEnabled && imageInfo.Platform.IsExternalFromImage)
                {
                    // Ensure latest base image exists locally before building
                    ExecuteHelper.ExecuteWithRetry("docker", $"pull {imageInfo.Platform.FromImage}", Options.IsDryRun);
                }

                ExecuteHelper.Execute(
                    "docker",
                    $"build -t {string.Join(" -t ", imageInfo.Tags)} {imageInfo.Platform.Model.Dockerfile}",
                    Options.IsDryRun);
            }
        }

        private static void ExecuteBuild()
        {
            BuildImages();
            RunTests();
            PushImages();
            WriteBuildSummary();
        }

        private static void ExecuteManifest()
        {
            WriteHeading("GENERATING MANIFESTS");
            foreach (ImageInfo imageInfo in RepoInfo.Images)
            {
                foreach (string tag in imageInfo.Model.SharedTags)
                {
                    string manifestYml =
$@"image: {RepoInfo.Model.DockerRepo}:{tag}
manifests:
";
                    foreach (KeyValuePair<string, Platform> kvp in imageInfo.Model.Platforms)
                    {
                        manifestYml +=
$@"  -
    image: {RepoInfo.Model.DockerRepo}:{kvp.Value.Tags.First()}
    platform:
      architecture: amd64
      os: {kvp.Key}
";
                    }

                    Console.WriteLine($"-- PUBLISHING MANIFEST:{Environment.NewLine}{manifestYml}");
                    File.WriteAllText("manifest.yml", manifestYml);
                    ExecuteHelper.Execute(
                        "manifest-tool",
                        $"--username {Options.Username} --password {Options.Password} push from-spec manifest.yml",
                        Options.IsDryRun);
                }
            }
        }

        private static void InitializeRepoInfo()
        {
            WriteHeading("READING REPO INFO");
            RepoInfo = RepoInfo.Create(Options.RepoInfo);
            Console.WriteLine(RepoInfo);
        }

        private static void RunTests()
        {
            if (!Options.IsTestRunDisabled)
            {
                WriteHeading("TESTING IMAGES");
                foreach (string command in RepoInfo.TestCommands)
                {
                    string[] parts = command.Split(' ');
                    ExecuteHelper.Execute(
                        parts[0],
                        command.Substring(parts[0].Length + 1),
                        Options.IsDryRun);
                }
            }
        }

        private static void PushImages()
        {
            if (Options.IsPushEnabled)
            {
                WriteHeading("PUSHING IMAGES");

                if (Options.Username != null)
                {
                    string loginArgsWithoutPassword = $"login -u {Options.Username} -p";
                    ExecuteHelper.Execute(
                        "docker",
                        $"{loginArgsWithoutPassword} {Options.Password}",
                        Options.IsDryRun,
                        executeMessageOverride: $"{loginArgsWithoutPassword} ********");
                }

                foreach (string tag in RepoInfo.GetPlatformTags())
                {
                    ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", Options.IsDryRun);
                }

                if (Options.Username != null)
                {
                    ExecuteHelper.Execute("docker", $"logout", Options.IsDryRun);
                }
            }
        }

        private static void WriteBuildSummary()
        {
            WriteHeading("IMAGES BUILT");
            foreach (string tag in RepoInfo.GetPlatformTags())
            {
                Console.WriteLine(tag);
            }
        }

        private static void WriteHeading(string heading)
        {
            Console.WriteLine();
            Console.WriteLine(heading);
            Console.WriteLine(new string('-', heading.Length));
        }
    }
}
