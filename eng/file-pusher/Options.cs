// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;

namespace FilePusher
{
    public class Options
    {
        public IEnumerable<string> Filters { get; set; }
        public string GitEmail { get; set; }
        public string GitAuthToken { get; set; }
        public string GitUser { get; set; }
        public string ConfigPath { get; set; }

        public static IEnumerable<Symbol> GetCliOptions() =>
            new Symbol[]
            {
                new Option<string[]>("--filter", () => Array.Empty<string>(),
                    "Filter to apply to repositories of the config json - wildcard chars * and ? supported")
                {
                    Name = nameof(Filters),
                    AllowMultipleArgumentsPerToken = false
                },
                new Argument<string>(nameof(ConfigPath), "Path to the config json file"),
                new Argument<string>(nameof(GitUser), "GitHub user used to make PR"),
                new Argument<string>(nameof(GitEmail), "GitHub email used to make PR"),
                new Argument<string>(nameof(GitAuthToken), "GitHub authorization token used to make PR")
            };
    }
}
