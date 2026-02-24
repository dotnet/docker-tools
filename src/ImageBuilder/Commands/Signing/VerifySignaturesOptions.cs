// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

public class VerifySignaturesOptions : Options
{
    public string ImageInfoPath { get; set; } = string.Empty;
    public RegistryOptions RegistryOverride { get; set; } = new();

    /// <summary>
    /// Base directory for trust materials (root CA certs and trust policy files).
    /// Defaults to /notation-trust which is baked into the container image.
    /// </summary>
    public string TrustMaterialsPath { get; set; } = "/notation-trust";
}

public class VerifySignaturesOptionsBuilder : CliOptionsBuilder
{
    private readonly RegistryOptionsBuilder _registryOptionsBuilder = new(isOverride: true);

    public override IEnumerable<Argument> GetCliArguments() =>
        base.GetCliArguments()
            .Concat(
            [
                new Argument<string>(nameof(VerifySignaturesOptions.ImageInfoPath),
                    "Path to merged image info file containing images to verify")
            ]);

    public override IEnumerable<Option> GetCliOptions() =>
        base.GetCliOptions()
            .Concat(_registryOptionsBuilder.GetCliOptions());
}
