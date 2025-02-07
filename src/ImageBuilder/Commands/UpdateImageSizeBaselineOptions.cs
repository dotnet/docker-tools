// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class UpdateImageSizeBaselineOptions : ImageSizeOptions
    {
        public bool AllBaselineData { get; set; }
    }

    public class UpdateImageSizeBaselineOptionsBuilder : ImageSizeOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption<bool>("all", nameof(UpdateImageSizeBaselineOptions.AllBaselineData),
                            "Updates baseline for all images regardless of size variance")
                    });
    }
}
#nullable disable

