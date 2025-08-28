// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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
public class OrasClient : IOrasClient
{
    private const string OrasExecutable = "oras";

    public string RunOrasCommand(IEnumerable<string> args, bool isDryRun = false)
    {
        return ExecuteHelper.Execute(
            fileName: OrasExecutable,
            args: string.Join(' ', args),
            isDryRun: isDryRun);
    }
}
