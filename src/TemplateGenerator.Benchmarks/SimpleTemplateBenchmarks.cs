// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator.Benchmarks;

[MemoryDiagnoser]
public class SimpleTemplateBenchmarks
{
    [Benchmark]
    public string GenerateSimpleTemplate()
    {
        return TemplateGenerator.GenerateCottleTemplate();
    }
}
