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
using System.Text.RegularExpressions;
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
            PullBaseImages();
            BuildImages();
            RunTests();
            PushImages();
            WriteBuildSummary();
        }

        private static void BuildImages()
        {
            WriteHeading("BUILDING IMAGES");
            foreach (ImageInfo image in Manifest.ActiveImages)
            {
                string dockerfilePath;
                bool createdPrivateDockerfile = UpdateDockerfileFromCommands(image, out dockerfilePath);

                try
                {
                    string tagArgs = $"-t {string.Join(" -t ", image.ActiveFullyQualifiedTags)}";
                    ExecuteHelper.Execute(
                        "docker",
                        $"build {tagArgs} -f {dockerfilePath} {image.ActivePlatform.Model.Dockerfile}",
                        Options.IsDryRun);
                }
                finally
                {
                    if (createdPrivateDockerfile)
                    {
                        File.Delete(dockerfilePath);
                    }
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
                    foreach (string tag in image.SharedFullyQualifiedTags)
                    {
                        StringBuilder manifestYml = new StringBuilder();
                        manifestYml.AppendLine($"image: {tag}");
                        manifestYml.AppendLine("manifests:");

                        foreach (Platform platform in image.Model.Platforms)
                        {
                            string platformTag = Manifest.Model.SubstituteTagVariables(platform.Tags.First());
                            manifestYml.AppendLine($"  -");
                            manifestYml.AppendLine($"    image: {repo.Name}:{platformTag}");
                            manifestYml.AppendLine($"    platform:");
                            manifestYml.AppendLine($"      architecture: {platform.Architecture.ToString().ToLowerInvariant()}");
                            manifestYml.AppendLine($"      os: {platform.OS}");
                            if (platform.Variant != null)
                            {
                                manifestYml.AppendLine($"      variant: {platform.Variant}");
                            }
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

        private static void PullBaseImages()
        {
            if (!Options.IsSkipPullingEnabled)
            {
                WriteHeading("PULLING LATEST BASE IMAGES");
                IEnumerable<string> fromImages = Manifest.ActiveImages
                    .SelectMany(image => image.ActivePlatform.FromImages)
                    .Where(Manifest.IsExternalImage)
                    .Distinct();
                foreach (string fromImage in fromImages)
                {
                    ExecuteHelper.ExecuteWithRetry("docker", $"pull {fromImage}", Options.IsDryRun);
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

                foreach (string tag in Manifest.ActivePlatformFullyQualifiedTags)
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
            WriteHeading("READING MANIFEST");
            Manifest = ManifestInfo.Create(
                Options.Manifest, Options.Architecture, Options.Repo, Options.Path, Options.RepoOwner);
            Console.WriteLine(JsonConvert.SerializeObject(Manifest, Formatting.Indented));
        }

        private static void RunTests()
        {
            if (!Options.IsTestRunDisabled)
            {
                WriteHeading("TESTING IMAGES");
                IEnumerable<string> testCommands = Manifest.TestCommands
                    .Select(command => Utilities.SubstituteVariables(Options.TestVariables, command));
                foreach (string command in testCommands)
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

        private static bool UpdateDockerfileFromCommands(ImageInfo image, out string dockerfilePath)
        {
            dockerfilePath = Path.Combine(image.ActivePlatform.Model.Dockerfile, "Dockerfile");

            // If an alternative repo owner was specified, update the intra-repo FROM commands.
            bool updateDockerfile = !string.IsNullOrWhiteSpace(Options.RepoOwner)
                && !image.ActivePlatform.FromImages.All(Manifest.IsExternalImage);
            if (updateDockerfile)
            {
                string dockerfileContents = File.ReadAllText(dockerfilePath);

                IEnumerable<string> fromImages = image.ActivePlatform.FromImages
                    .Where(fromImage => !Manifest.IsExternalImage(fromImage));
                foreach (string fromImage in fromImages)
                {
                    Regex fromRegex = new Regex($@"FROM\s+{Regex.Escape(fromImage)}[^\S\r\n]*");
                    string newFromImage = DockerHelper.ReplaceImageOwner(fromImage, Options.RepoOwner);
                    Console.WriteLine($"Replacing FROM `{fromImage}` with `{newFromImage}`");
                    dockerfileContents = fromRegex.Replace(dockerfileContents, $"FROM {newFromImage}");
                }

                // Don't overwrite the original dockerfile - write it to a new path.
                dockerfilePath = Path.Combine(image.ActivePlatform.Model.Dockerfile, ".Dockerfile");
                Console.WriteLine($"Writing updated Dockerfile: {dockerfilePath}");
                Console.WriteLine(dockerfileContents);
                File.WriteAllText(dockerfilePath, dockerfileContents);
            }

            return updateDockerfile;
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
                    new Uri($"https://cloud.docker.com/v2/repositories/{repo.Name}/"));

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

        private static void WriteBuildSummary()
        {
            WriteHeading("IMAGES BUILT");
            foreach (string tag in Manifest.ActivePlatformFullyQualifiedTags)
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
