using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Stratum.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.NativeAot80)]
public class ConcurrentOperationsBenchmarks
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
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    [Arguments(500)]
    public async Task ConcurrentWrites(int concurrency)
    {
        var tasks = new List<Task>();
        var size = 1024 * 1024; // 1MB

        for (int i = 0; i < concurrency; i++)
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
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    [Arguments(500)]
    public async Task ConcurrentReads(int concurrency)
    {
        // First create test files
        var size = 1024 * 1024; // 1MB
        var keys = new List<string>();

        for (int i = 0; i < concurrency; i++)
        {
            var data = new byte[size];
            Random.Shared.NextBytes(data);
            var key = $"test-{Guid.NewGuid()}";

            var filePath = Path.Combine(_dataDirectory, key);
            await File.WriteAllBytesAsync(filePath, data);
            keys.Add(key);
        }

        // Benchmark concurrent reads
        var readTasks = new List<Task>();
        foreach (var key in keys)
        {
            readTasks.Add(Task.Run(async () =>
            {
                var filePath = Path.Combine(_dataDirectory, key);
                await File.ReadAllBytesAsync(filePath);
            }));
        }

        await Task.WhenAll(readTasks);
    }
}
