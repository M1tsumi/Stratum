using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Stratum.Domain.Entities;

namespace Stratum.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class MetadataQueryBenchmarks
{
    private List<ObjectMetadata> _metadataList = new();
    private Dictionary<string, ObjectMetadata> _metadataDict = new();

    [GlobalSetup]
    public void Setup()
    {
        // Create test metadata objects
        for (int i = 0; i < 10000; i++)
        {
            var metadata = new ObjectMetadata(
                "test-bucket",
                $"test-{i:D5}",
                Guid.NewGuid().ToString(),
                1024,
                "application/octet-stream",
                DateTime.UtcNow);

            _metadataList.Add(metadata);
            _metadataDict[metadata.Key] = metadata;
        }
    }

    [Benchmark]
    public ObjectMetadata GetMetadata()
    {
        var key = $"test-{Random.Shared.Next(0, 10000):D5}";
        return _metadataDict.TryGetValue(key, out var metadata) ? metadata : null!;
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public List<ObjectMetadata> ListMetadata(int count)
    {
        return _metadataList.Take(count).ToList();
    }

    [Benchmark]
    public Bucket CreateBucket()
    {
        return new Bucket(
            $"bucket-{Guid.NewGuid()}",
            "us-east-1");
    }

    [Benchmark]
    public string GenerateETag()
    {
        return Guid.NewGuid().ToString();
    }
}
