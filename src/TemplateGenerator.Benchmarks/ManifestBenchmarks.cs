// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.DotNet.ImageBuilder.ReadModel;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator.Benchmarks;

[MemoryDiagnoser]
public class ManifestBenchmarks
{
    private static readonly string s_manifestPath =
        Environment.GetEnvironmentVariable("MANIFEST_PATH") ?? "manifest.json";

    [Benchmark]
    public async Task<string> RoundTripManifestSerialization()
    {
        ManifestInfo manifest = await ManifestInfo.LoadAsync(s_manifestPath);
        return manifest.ToJsonString();
    }
}
