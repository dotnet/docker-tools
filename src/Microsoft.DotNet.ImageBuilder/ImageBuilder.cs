// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageBuilder
    {
        private static Options Options { get; set; }
        private static ManifestInfo Manifest { get; set; }

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
                    ReadManifest();

                    switch (Options.Command)
                    {
                        case CommandType.Build:
                            Build();
                            break;
                        case CommandType.PublishManifest:
                            PublishManifest();
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                result = 1;
            }

            return result;
        }

        private static void Build()
        {
            BuildImages();
            RunTests();
            PushImages();
            WriteBuildSummary();
        }

        private static void BuildImages()
        {
            WriteHeading("BUILDING IMAGES");
            foreach (ImageInfo image in Manifest.Images.Where(image => image.Platform != null))
            {
                Console.WriteLine($"-- BUILDING: {image.Platform.Model.Dockerfile}");
                if (!Options.IsSkipPullingEnabled && image.Platform.IsExternalFromImage)
                {
                    // Ensure latest base image exists locally before building
                    ExecuteHelper.ExecuteWithRetry("docker", $"pull {image.Platform.FromImage}", Options.IsDryRun);
                }

                ExecuteHelper.Execute(
                    "docker",
                    $"build -t {string.Join(" -t ", image.Tags)} {image.Platform.Model.Dockerfile}",
                    Options.IsDryRun);
            }
        }

        private static void RunTests()
        {
            if (!Options.IsTestRunDisabled)
            {
                WriteHeading("TESTING IMAGES");
                foreach (string command in Manifest.TestCommands)
                {
                    int firstSpaceIndex = command.IndexOf(' ');
                    ExecuteHelper.Execute(
                        command.Substring(0, firstSpaceIndex),
                        command.Substring(firstSpaceIndex + 1),
                        Options.IsDryRun);
                }
            }
        }

        private static void PublishManifest()
        {
            WriteHeading("GENERATING MANIFESTS");
            foreach (ImageInfo image in Manifest.Images)
            {
                foreach (string tag in image.Model.SharedTags)
                {
                    StringBuilder manifestYml = new StringBuilder();
                    manifestYml.AppendLine($"image: {Manifest.Model.DockerRepo}:{tag}");
                    manifestYml.AppendLine("manifests:");

                    foreach (KeyValuePair<string, Platform> kvp in image.Model.Platforms)
                    {
                        manifestYml.AppendLine($"  -");
                        manifestYml.AppendLine($"    image: {Manifest.Model.DockerRepo}:{kvp.Value.Tags.First()}");
                        manifestYml.AppendLine($"    platform:");
                        manifestYml.AppendLine($"      architecture: amd64");
                        manifestYml.AppendLine($"      os: {kvp.Key}");
                    }

                    Console.WriteLine($"-- PUBLISHING MANIFEST:{Environment.NewLine}{manifestYml}");
                    File.WriteAllText("manifest.yml", manifestYml.ToString());
                    ExecuteHelper.Execute(
                        "manifest-tool",
                        $"--username {Options.Username} --password {Options.Password} push from-spec manifest.yml",
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

                foreach (string tag in Manifest.GetPlatformTags())
                {
                    ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", Options.IsDryRun);
                }

                if (Options.Username != null)
                {
                    ExecuteHelper.Execute("docker", $"logout", Options.IsDryRun);
                }
            }
        }

        private static void ReadManifest()
        {
            WriteHeading("READING Manifest");
            Manifest = ManifestInfo.Create(Options.Manifest);
            Console.WriteLine(Manifest);
        }

        private static void WriteBuildSummary()
        {
            WriteHeading("IMAGES BUILT");
            foreach (string tag in Manifest.GetPlatformTags())
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
