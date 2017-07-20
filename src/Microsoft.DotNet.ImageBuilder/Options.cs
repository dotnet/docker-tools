// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public class Options
    {
        public const string Usage = @"Docker Image Builder

Summary:  Builds all Dockerfiles detected in the current folder and sub-folders in the correct order to satisfy cross dependencies.

Usage:  image-builder [options]

Options:
      --repo-owner                      An alternative repo owner which overrides what is specified in the manifest
      --architecture                    The architecture of the Docker images to build (default is the current OS architecture)
      --command                         Build command to execute (Build/PublishManifest/UpdateReadme)
      --dry-run                         Dry run of what images get built and order they would get built in
  -h, --help                            Show help information
      --manifest                        Path to json file which describes the repo
      --repo                            Repo to build (Default is to build all)
      --password                        Password for the Docker registry the images are pushed to
      --path                            Path of the directory to build (Default is to build all)
      --push                            Push built images to Docker registry
      --skip-pulling                    Skip explicitly pulling the base images of the Dockerfiles
      --skip-test                       Skip running the tests
      --test-var list                   Named variables to substitute into the test commands (name=value)
      --username                        Username for the Docker registry the images are pushed to
";

        public string RepoOwner { get; private set; }
        public Architecture Architecture { get; private set; } = DockerHelper.GetArchitecture();
        public CommandType Command { get; private set; }
        public bool IsDryRun { get; private set; }
        public bool IsHelpRequest { get; private set; }
        public bool IsPushEnabled { get; private set; }
        public bool IsSkipPullingEnabled { get; private set; }
        public bool IsTestRunDisabled { get; private set; }
        public string Manifest { get; private set; } = "manifest.json";
        public string Repo { get; private set; }
        public string Password { get; private set; }
        public string Path { get; private set; }
        public IDictionary<string, string> TestVariables { get; private set; } = new Dictionary<string, string>();
        public string Username { get; private set; }

        private Options()
        {
        }

        public static Options ParseArgs(string[] args)
        {
            Options options = new Options();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "--repo-owner", StringComparison.Ordinal))
                {
                    options.RepoOwner = GetArgValue(args, ref i, "repo-owner");
                }
                else if (string.Equals(arg, "--architecture", StringComparison.Ordinal))
                {
                    string architecture = GetArgValue(args, ref i, "architecture");
                    options.Architecture = (Architecture)Enum.Parse(typeof(Architecture), architecture, true);
                }
                else if (string.Equals(arg, "--command", StringComparison.Ordinal))
                {
                    string commandType = GetArgValue(args, ref i, "command");
                    options.Command = (CommandType)Enum.Parse(typeof(CommandType), commandType, true);
                }
                else if (string.Equals(arg, "--dry-run", StringComparison.Ordinal))
                {
                    options.IsDryRun = true;
                }
                else if (string.Equals(arg, "-h", StringComparison.Ordinal)
                    || string.Equals(arg, "--help", StringComparison.Ordinal))
                {
                    options.IsHelpRequest = true;
                }
                else if (string.Equals(arg, "--manifest", StringComparison.Ordinal))
                {
                    options.Manifest = GetArgValue(args, ref i, "manifest");
                }
                else if (string.Equals(arg, "--repo", StringComparison.Ordinal))
                {
                    options.Repo = GetArgValue(args, ref i, "repo");
                }
                else if (string.Equals(arg, "--push", StringComparison.Ordinal))
                {
                    options.IsPushEnabled = true;
                }
                else if (string.Equals(arg, "--password", StringComparison.Ordinal))
                {
                    options.Password = GetArgValue(args, ref i, "password");
                }
                else if (string.Equals(arg, "--path", StringComparison.Ordinal))
                {
                    options.Path = GetArgValue(args, ref i, "path");
                }
                else if (string.Equals(arg, "--username", StringComparison.Ordinal))
                {
                    options.Username = GetArgValue(args, ref i, "username");
                }
                else if (string.Equals(arg, "--skip-pulling", StringComparison.Ordinal))
                {
                    options.IsSkipPullingEnabled = true;
                }
                else if (string.Equals(arg, "--skip-test", StringComparison.Ordinal))
                {
                    options.IsTestRunDisabled = true;
                }
                else if (string.Equals(arg, "--test-var", StringComparison.Ordinal))
                {
                    IEnumerable<string> values = GetArgValues(args, ref i, "test-var");
                    options.TestVariables = ParseNameValuePairs(values);
                }
                else
                {
                    throw new ArgumentException($"Unknown argument: '{arg}'{Environment.NewLine}{Usage}");
                }
            }

            return options;
        }

        private static string GetArgValue(string[] args, ref int i, string argName)
        {
            if (!IsNextArgValue(args, i))
            {
                throw GetArgValueNotFoundException(argName);
            }

            i++;
            return args[i];
        }

        private static IEnumerable<string> GetArgValues(string[] args, ref int i, string argName)
        {
            List<string> values = new List<string>();

            while (IsNextArgValue(args, i))
            {
                i++;
                values.Add(args[i]);
            }

            if (!values.Any())
            {
                throw GetArgValueNotFoundException(argName);
            }

            return values;
        }

        private static Exception GetArgValueNotFoundException(string argName)
        {
            return new ArgumentException($"No value specified for option '{argName}'.{Environment.NewLine}{Usage}");
        }

        private static bool IsNextArgValue(string[] args, int i)
        {
            return i + 1 < args.Length && !args[i + 1].StartsWith("-");
        }

        private static IDictionary<string, string> ParseNameValuePairs(IEnumerable<string> nameValuePairs)
        {
            return nameValuePairs
                .Select(pair => pair.Split(new char[] { '=' }, 2))
                .ToDictionary(split => split[0], split => split[1]);
        }
    }
}
