// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.DotNet.ImageBuilder.Models.Oci;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface IOrasClient
{
    string RunOrasCommand(IEnumerable<string> args, bool isDryRun);

    public Descriptor GetDescriptor(string digest, bool isDryRun)
    {
        string output = RunOrasCommand(
            args: [
                "manifest",
                "fetch",
                "--descriptor",
                digest
            ],
            isDryRun: isDryRun);

        return Descriptor.FromJson(output);
    }
}

[Export(typeof(IOrasClient))]
public class OrasClient : IOrasClient
{
    private const string OrasExecutable = "oras";
    private const string JsonFormatArg = "--format json";

    public string RunOrasCommand(IEnumerable<string> args, bool isDryRun = false)
    {
        return ExecuteHelper.Execute(
            fileName: OrasExecutable,
            args: string.Join(' ', [..args, JsonFormatArg]),
            isDryRun: isDryRun);
    }
}
