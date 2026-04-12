using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Stratum.Domain.Entities;

namespace Stratum.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class ObjectUploadBenchmarks
{
    private string _dataDirectory = "./benchmark-data";

    [GlobalSetup]
    public void Setup()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
        Directory.CreateDirectory(_dataDirectory);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
    }

    [Benchmark]
    [Arguments(1024)] // 1KB
    [Arguments(4096)] // 4KB
    [Arguments(16384)] // 16KB
    public async Task WriteFile(int size)
    {
        var data = new byte[size];
        Random.Shared.NextBytes(data);
        var key = $"test-{Guid.NewGuid()}";

        var filePath = Path.Combine(_dataDirectory, key);
        await File.WriteAllBytesAsync(filePath, data);
    }

    [IterationSetup(Target = nameof(WriteMultipleFiles))]
    public void SetupWriteMultipleFiles()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
        Directory.CreateDirectory(_dataDirectory);
    }

    [IterationCleanup(Target = nameof(WriteMultipleFiles))]
    public void CleanupWriteMultipleFiles()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    public async Task WriteMultipleFiles(int count)
    {
        var tasks = new List<Task>();
        var size = 1024; // 1KB

        for (int i = 0; i < count; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var data = new byte[size];
                Random.Shared.NextBytes(data);
                var key = $"test-{Guid.NewGuid()}";

                var filePath = Path.Combine(_dataDirectory, key);
                await File.WriteAllBytesAsync(filePath, data);
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public ObjectMetadata CreateMetadata()
    {
        return new ObjectMetadata(
            "test-bucket",
            $"test-{Guid.NewGuid()}",
            Guid.NewGuid().ToString(),
            1024 * 1024,
            "application/octet-stream",
            DateTime.UtcNow);
    }
}
