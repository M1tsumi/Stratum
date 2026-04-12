using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Stratum.Benchmarks.Benchmarks;

[MemoryDiagnoser]
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

    [IterationSetup(Target = nameof(ConcurrentWrites))]
    public void SetupConcurrentWrites()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
        Directory.CreateDirectory(_dataDirectory);
    }

    [IterationCleanup(Target = nameof(ConcurrentWrites))]
    public void CleanupConcurrentWrites()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
    }

    [IterationSetup(Target = nameof(ConcurrentReads))]
    public void SetupConcurrentReads()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
        Directory.CreateDirectory(_dataDirectory);

        // Pre-populate with files for reading
        for (int i = 0; i < 100; i++)
        {
            var data = new byte[1024];
            Random.Shared.NextBytes(data);
            var key = $"test-{i}";
            var filePath = Path.Combine(_dataDirectory, key);
            File.WriteAllBytes(filePath, data);
        }
    }

    [IterationCleanup(Target = nameof(ConcurrentReads))]
    public void CleanupConcurrentReads()
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
    public async Task ConcurrentWrites(int concurrency)
    {
        var tasks = new List<Task>();
        var size = 1024; // 1KB

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
    public async Task ConcurrentReads(int concurrency)
    {
        var tasks = new List<Task>();

        for (int i = 0; i < concurrency; i++)
        {
            var key = $"test-{i % 100}"; // Cycle through pre-populated files
            tasks.Add(Task.Run(async () =>
            {
                var filePath = Path.Combine(_dataDirectory, key);
                await File.ReadAllBytesAsync(filePath);
            }));
        }

        await Task.WhenAll(tasks);
    }
}
