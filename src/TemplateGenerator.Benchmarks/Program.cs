using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.DotNet.ImageBuilder.ReadModel;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator;

[MemoryDiagnoser]
public class Benchmarks
{
    private static readonly string s_manifestPath =
        Environment.GetEnvironmentVariable("MANIFEST_PATH") ?? "manifest.json";

    [Benchmark]
    public async Task<string> RoundTripManifestSerialization()
    {
        ManifestInfo manifest = await ManifestInfo.LoadAsync(s_manifestPath);
        return manifest.ToJsonString();
    }

    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<Benchmarks>();
    }
}
