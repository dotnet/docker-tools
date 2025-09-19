// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator.Benchmarks;

[MemoryDiagnoser]
public class GenerateDockerfilesBenchmarks
{
    private static readonly string s_manifestPath =
        Environment.GetEnvironmentVariable("MANIFEST_PATH") ?? "manifest.json";

    [Benchmark]
    public async Task<string> GenerateDockerfiles()
    {
        await new TemplateGeneratorCli().GenerateDockerfiles(s_manifestPath);
        return "Completed";
    }
}
