// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;

namespace YamlUpdater
{
    public class Options
    {
        public string ConfigPath { get; set; } = string.Empty;
        public string NodeQueryPath { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string GitEmail { get; set; } = string.Empty;
        public string GitAuthToken { get; set; } = string.Empty;
        public string GitUser { get; set; } = string.Empty;
        public string GitRepo { get; set; } = string.Empty;
        public string GitBranch { get; set; } = string.Empty;
        public string GitOwner { get; set; }  = string.Empty;

        public static IEnumerable<Symbol> GetCliOptions() =>
            new Symbol[]
            {
                new Argument<string>(nameof(ConfigPath), "Path to the config json file"),
                new Argument<string>(nameof(NodeQueryPath), "Query path of the YAML node to update"),
                new Argument<string>(nameof(NewValue), "Value to set the YAML node to"),
                new Argument<string>(nameof(GitUser), "GitHub user used to make PR"),
                new Argument<string>(nameof(GitEmail), "GitHub email used to make PR"),
                new Argument<string>(nameof(GitAuthToken), "GitHub authorization token used to make PR"),
                new Argument<string>(nameof(GitOwner), "Owner of the GitHub repo"),
                new Argument<string>(nameof(GitRepo), "Name of the GitHub repo"),
                new Argument<string>(nameof(GitBranch), "Name of the GitHub repo branch")
            };
    }
}
