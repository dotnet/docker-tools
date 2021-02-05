// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyBaseImagesOptions : CopyImagesOptions
    {
        public IDictionary<string, ImportSourceCredentials> SourceCredentials { get; set; } =
            new Dictionary<string, ImportSourceCredentials>();
    }

    public class CopyBaseImagesOptionsBuilder : CopyImagesOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(new Option[]
                {
                    CreateDictionaryOption("source-creds", nameof(CopyBaseImagesOptions.SourceCredentials),
                        "Named credentials that map to a source registry ((<registry>=<username>;<password>)",
                        val =>
                            {
                                (string username, string password) = val.ParseKeyValuePair(';');
                                return new ImportSourceCredentials(password, username);
                            })
                });
    }
}
#nullable disable
