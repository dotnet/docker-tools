// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
                        case CommandType.UpdateReadme:
                            UpdateReadmesAsync().Wait();
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
                    $"build -t {string.Join(" -t ", image.AllTags)} {image.Platform.Model.Dockerfile}",
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
                    string filename;
                    string args;

                    int firstSpaceIndex = command.IndexOf(' ');
                    if (firstSpaceIndex == -1)
                    {
                        filename = command;
                        args = null;
                    }
                    else
                    {
                        filename = command.Substring(0, firstSpaceIndex);
                        args = command.Substring(firstSpaceIndex + 1);
                    }

                    ExecuteHelper.Execute(filename, args, Options.IsDryRun);
                }
            }
        }

        private static void PublishManifest()
        {
            WriteHeading("GENERATING MANIFESTS");
            foreach (RepoInfo repo in Manifest.Repos)
            {
                foreach (ImageInfo image in repo.Images)
                {
                    foreach (string tag in image.SharedTags)
                    {
                        StringBuilder manifestYml = new StringBuilder();
                        manifestYml.AppendLine($"image: {tag}");
                        manifestYml.AppendLine("manifests:");

                        foreach (KeyValuePair<string, Platform> kvp in image.Model.Platforms)
                        {
                            string platformTag = Manifest.Model.SubstituteTagVariables(kvp.Value.Tags.First());
                            manifestYml.AppendLine($"  -");
                            manifestYml.AppendLine($"    image: {repo.Model.Name}:{platformTag}");
                            manifestYml.AppendLine($"    platform:");
                            manifestYml.AppendLine($"      architecture: amd64");
                            manifestYml.AppendLine($"      os: {kvp.Key}");
                        }

                        Console.WriteLine($"-- PUBLISHING MANIFEST:{Environment.NewLine}{manifestYml}");
                        File.WriteAllText("manifest.yml", manifestYml.ToString());

                        // ExecuteWithRetry because the manifest-tool fails periodically with communicating 
                        // with the Docker Registry.
                        ExecuteHelper.ExecuteWithRetry(
                            "manifest-tool",
                            $"--username {Options.Username} --password {Options.Password} push from-spec manifest.yml",
                            Options.IsDryRun);
                    }
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

                foreach (string tag in Manifest.PlatformTags)
                {
                    ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", Options.IsDryRun);
                }

                if (Options.Username != null)
                {
                    ExecuteHelper.Execute("docker", $"logout", Options.IsDryRun);
                }
            }
        }

        private static async Task UpdateReadmesAsync()
        {
            WriteHeading("UPDATING READMES");
            foreach (RepoInfo repo in Manifest.Repos)
            {
                // Docker Hub/Cloud API is not documented thus it is subject to change.  This is the only option
                // until a supported API exists.
                HttpRequestMessage request = new HttpRequestMessage(
                    new HttpMethod("PATCH"),
                    new Uri($"https://cloud.docker.com/v2/repositories/{repo.Model.Name}/"));

                string credentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes($"{Options.Username}:{Options.Password}"));
                request.Headers.Add("Authorization", $"Basic {credentials}");

                JObject jsonContent = new JObject(new JProperty("full_description", new JValue(repo.GetReadmeContent())));
                request.Content = new StringContent(jsonContent.ToString(), Encoding.UTF8, "application/json");

                if (!Options.IsDryRun)
                {
                    HttpResponseMessage response = await new HttpClient().SendAsync(request);
                    Console.WriteLine($"-- RESPONSE:{Environment.NewLine}{response}");
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        private static void ReadManifest()
        {
            WriteHeading("READING MANIFEST");
            Manifest = ManifestInfo.Create(Options.Manifest);
            Console.WriteLine(JsonConvert.SerializeObject(Manifest, Formatting.Indented));
        }

        private static void WriteBuildSummary()
        {
            WriteHeading("IMAGES BUILT");
            foreach (string tag in Manifest.PlatformTags)
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
