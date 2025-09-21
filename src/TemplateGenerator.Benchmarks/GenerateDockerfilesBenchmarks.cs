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
    public void GenerateDockerfiles()
    {
        var generator = new TemplateGeneratorCli();
        generator.GenerateDockerfiles(s_manifestPath);
    }

    [Benchmark]
    public void GenerateReadmes()
    {
        var generator = new TemplateGeneratorCli();
        generator.GenerateReadmes(s_manifestPath);
    }

    [Benchmark]
    public void GenerateAll()
    {
        var generator = new TemplateGeneratorCli();
        generator.GenerateAll(s_manifestPath);
    }
}
